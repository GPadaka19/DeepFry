# LabManagement — Development Plan

## Project Goal

Build a lightweight Windows laboratory management system consisting of:

- `LabManagement.Host`
  - Runs on the presentation/lecturer PC in each lab.
  - Used exclusively by UPT Lab staff.
  - Acts as the central management/control application.
  - Uses its local `10.x.x.90` address to scan Client IPs `.1` through `.89`.
  - Connects outbound to Client listeners on TCP port `5020`.
  - Displays all connected laboratory PCs.
  - Eventually sends administrative commands to Clients.

- `LabManagement.Client`
  - Runs on every student PC.
  - Runs as a lightweight background application/service.
  - Automatically identifies itself using Windows `Environment.MachineName`.
  - Listens for the Host on TCP port `5020`.
  - Lab network convention:
    - `10.xx.x.x`
    - Host always uses `.90`
    - Example: `10.22.4.13` → Host `10.22.4.90`
  - Maintains the TCP session initiated by Host.
  - Sends periodic heartbeat.
  - Eventually executes UWF commands locally.

The application must remain lightweight and suitable for deployment across many laboratory PCs.

---

# Current Architecture

```text
                    LAB NETWORK
                         |
             +-----------+-----------+
             |                       |
             v                       v
     LabManagement.Host      LabManagement.Client
        PC Host (.90)          Student PC (.1-.89)
        subnet scanner  -----------> TCP listener : 5020
             |<----------------------|
                REGISTER / HEARTBEAT
```

Current heartbeat configuration:

- Heartbeat interval: `2 seconds`
- Stale timeout: `6 seconds`
- Host stale monitor interval: `1 second`
- TCP port: `5020`

---

# Phase 0 — Protect Current Lifecycle

## Status

DONE.

The current lifecycle implementation has already been fixed and regression-tested.

Verified behavior:

1. Host discovers and connects to a Client listener.
2. Client sends REGISTER on the accepted connection.
3. Host creates active ClientInfo.
4. Client sends HEARTBEAT every 2 seconds.
5. Host updates `ClientInfo.LastHeartbeat`.
6. Client remains Online.
7. No heartbeat for >6 seconds → Offline.
8. TCP disconnect → immediate Offline.
9. Client reconnects → Online.
10. Old connection heartbeat cannot revive a newer connection.
11. Host tracks the active connection for each hostname.

Existing regression harness:

```text
tests/LabManagement.Host.LifecycleTests/Program.cs
```

Expected result:

```text
PASS: lifecycle, reconnect, malformed message, EOF, and framing checks.
```

Do not regress this behavior.

---

# Phase 1 — Inspect and Stabilize Current Code

## Status

DONE.

Completed:

1. Lifecycle regression harness covers REGISTER, HEARTBEAT, stale timeout,
   EOF, reconnect with a duplicate hostname, old-connection heartbeats,
   malformed registration/messages, and partial/consecutive framed messages.
2. `ClientRegistry` is the authoritative lifecycle module for active
   connections and client state. `MainWindow` only adapts its state to WPF.
3. A reconnect replaces the active connection for that hostname; subsequent
   state changes require the exact active connection reference.
4. The complete solution and the lifecycle harness build successfully.

Before adding new functionality:

1. Build the entire solution.
2. Run the existing lifecycle test.
3. Inspect:
   - `ClientConnection.cs`
   - `MainWindow.xaml.cs`
   - `ClientInfo`
   - current TCP listener
   - current Client Worker
4. Remove obvious duplicated state handling if safe.
5. Ensure there is exactly one authoritative active connection per hostname.
6. Ensure old connections cannot modify the state of a newer connection.

Do NOT implement UWF yet.

---

# Phase 2 — Introduce Shared Protocol Model

## Status

DONE.

`LabManagement.Protocol` is a shared .NET class library referenced by both
Host and Client. It owns `MessageType`, `RequestMessage`, `ResponseMessage`,
`ErrorInfo`, `RegisterPayload`, and the common JSON options. REGISTER and
HEARTBEAT now use this envelope; no command behavior has been added.

Goal:

Stop using ad-hoc strings and duplicated message classes.

Create a shared protocol representation.

Preferred structure:

```text
LabManagement.Protocol
```

If creating a third project is appropriate, use a shared class library.

Otherwise, keep the protocol model duplicated temporarily but make the structures identical.

Preferred message envelope:

```json
{
  "requestId": "uuid",
  "type": "heartbeat",
  "payload": {}
}
```

Response:

```json
{
  "requestId": "uuid",
  "type": "response",
  "success": true,
  "payload": {}
}
```

Error:

```json
{
  "requestId": "uuid",
  "type": "response",
  "success": false,
  "error": {
    "code": "COMMAND_FAILED",
    "message": "..."
  }
}
```

Recommended C# concepts:

```text
MessageType
RequestMessage
ResponseMessage
ErrorInfo
```

Keep the protocol simple.

Do not over-engineer it into a generic RPC framework.

---

# Phase 3 — Message Framing

## Status

DONE.

`JsonLineReader` keeps unread bytes between reads, supports partial and
consecutive newline-delimited messages, enforces a 64 KiB message limit, and
ends a malformed/incomplete connection safely. `JsonLineWriter` serializes
concurrent writes into complete JSON lines.

Current protocol uses newline-delimited JSON.

Keep this for now:

```text
JSON\n
```

Example:

```json
{"requestId":"...","type":"heartbeat","payload":{}}
```

followed by:

```text
\n
```

Implement robust framing:

- One message per line.
- Handle partial TCP reads.
- Handle multiple messages arriving in one TCP read.
- Handle EOF.
- Reject malformed JSON safely.
- Do not crash the connection listener because of one malformed message.

Do not switch to a custom binary protocol yet.

---

# Phase 4 — Bidirectional TCP Communication

## Status

DONE.

Host and Client now read and write over the same TCP connection. Until the
Client command dispatcher exists, an incoming command receives a structured
`COMMAND_NOT_IMPLEMENTED` response.

Current system primarily relies on Client → Host communication.

Upgrade it to true bidirectional communication.

Required behavior:

```text
Host
  |
  | Request
  v
Client
  |
  | Response
  v
Host
```

At the same time:

```text
Client
  |
  | Heartbeat
  v
Host
```

The same TCP connection should support both directions.

Host must be able to send commands through the existing Client connection.

Do NOT create a new TCP connection for every command.

---

# Phase 5 — Request/Response Infrastructure

## Status

DONE.

`ClientConnection.SendCommandAsync` registers a pending `requestId`, sends a
command over the active connection, and awaits the correlated response. It
supports timeout, cancellation, unknown response IDs, and pending-request
failure when the connection closes.

Implement a request tracking mechanism on Host.

Example:

```text
requestId = UUID
```

Host sends:

```json
{
  "requestId": "123",
  "type": "uwf.status",
  "payload": {}
}
```

Client responds:

```json
{
  "requestId": "123",
  "type": "response",
  "success": true,
  "payload": {
    "enabled": true
  }
}
```

Host should be able to:

```text
SendCommandAsync(...)
```

and await the corresponding response.

Requirements:

- request timeout
- cancellation
- unknown requestId handling
- disconnected Client handling
- command response correlation
- no deadlocks
- no blocking UI thread

Suggested timeout initially:

```text
10 seconds
```

---

# Phase 6 — Client Command Dispatcher

Create a command dispatcher on Client.

Conceptually:

```text
Incoming Message
       |
       v
CommandDispatcher
       |
       +---- uwf.status
       |
       +---- uwf.lock
       |
       +---- uwf.unlock
       |
       +---- system.restart
       |
       +---- system.shutdown
```

Do not directly execute commands from the TCP reader.

The TCP layer should only:

1. Receive message.
2. Parse message.
3. Pass command to dispatcher.
4. Dispatcher executes command.
5. Dispatcher returns response.

---

# Phase 7 — UWF Status Only

First UWF feature must be READ-ONLY.

Implement:

```text
uwf.status
```

Client executes the appropriate Windows UWF command locally.

The exact command should be verified against the target Windows 11 environment before implementation.

Expected response model:

```json
{
  "requestId": "...",
  "type": "response",
  "success": true,
  "payload": {
    "enabled": true
  }
}
```

If status cannot be determined:

```json
{
  "requestId": "...",
  "type": "response",
  "success": false,
  "error": {
    "code": "UWF_STATUS_FAILED",
    "message": "..."
  }
}
```

Do not implement LOCK/UNLOCK yet.

---

# Phase 8 — Host UI for UWF Status

Add UWF status to the Host client table.

Example:

```text
Computer       IP             Connection    UWF
------------------------------------------------
Komputer-01    10.22.4.1     Online        Locked
Komputer-02    10.22.4.2     Online        Locked
Komputer-03    10.22.4.3     Offline       Unknown
```

Possible states:

```text
Locked
Unlocked
Unknown
Checking
```

Do not infer UWF status from connection status.

UWF status must come from the Client.

---

# Phase 9 — UWF LOCK / UNLOCK

Only after `uwf.status` is reliable.

Implement:

```text
uwf.lock
uwf.unlock
```

The Client is responsible for executing the Windows UWF command.

Important:

- Never execute arbitrary shell commands received from Host.
- Use a strict command allowlist.
- `uwf.lock` maps only to the intended UWF command.
- `uwf.unlock` maps only to the intended UWF command.
- `system.restart` maps only to Windows restart and must be triggered separately
  from UWF Lock/Unlock after target selection and Host confirmation.
- Capture exit code.
- Capture stdout/stderr.
- Return structured result to Host.

---

# Phase 10 — Batch Operations

After single-client commands work:

Host should support selecting multiple Clients.

Example:

```text
[x] Komputer-01
[x] Komputer-02
[ ] Komputer-03
[x] Komputer-04

        [ LOCK SELECTED ]
```

Host sends commands independently to each selected active Client.

Requirements:

- parallel execution
- individual result per Client
- timeout per Client
- failed Client must not block successful Clients
- UI shows per-client result

Example:

```text
Komputer-01   Locked    ✓
Komputer-02   Locked    ✓
Komputer-03   Offline   —
Komputer-04   Failed    ✗
```

---

# Phase 11 — Authentication / Authorization

## Status

DONE FOR THE CURRENT INTERNAL-LAB POLICY.

Completed for Host application access:

- First run requires UPT Lab staff to create a per-lab password.
- Every subsequent Host launch requires that password before the dashboard opens.
- Password data is stored as PBKDF2-SHA256 hash data with a unique random salt,
  never as the original password, in
  `C:\ProgramData\LabManagement\host-settings.json`.
- The `Settings` button lets staff change the password after confirming the
  current password; no rebuild or republish is required.
- Network pairing keys are intentionally not used. Client requires no secret,
  `.env`, or per-PC configuration.
- Client only accepts the explicit command allowlist, and its automatic
  firewall rule restricts TCP 5020 to the local subnet.

Do not implement Client-side command passwords.

The deployment model is:

```text
UPT Lab Staff
      |
      v
Host PC
      |
      v
Student PCs
```

Host application access should eventually be protected.

Potentially:

```text
Host application
    |
    +-- Staff authentication
```

Client does not need an interactive password.

Accepted risk: the transport is not authenticated or encrypted. This design is
limited to the controlled internal lab network. Reintroducing authenticated
transport later would be a new policy decision, not a hidden deployment step.

---

# Phase 12 — Windows Service

## Status

IN PROGRESS.

Completed:

- Client uses the official .NET Windows Service lifetime when installed as a
  service named `LabManagement Client`.
- A normal manual launch is supported and requires no argument or pairing key.
- The published EXE requests Administrator privileges for UWF and firewall work.
- `scripts\Install-LabManagementClient.ps1` installs the service as Automatic
  start and configures Windows Service recovery to restart after a failure.

Still required:

- Production logging location and an uninstall/update script.
- Real-lab installation and restart/reconnect verification.

Only after networking and commands are stable.

Convert:

```text
LabManagement.Client.exe
```

into a Windows background service.

Requirements:

- Start automatically with Windows.
- Run without user interaction.
- Automatically reconnect.
- Restart safely after failure.
- No visible console window in production.
- Log errors appropriately.

Development mode may continue running as a normal executable.

---

# Phase 13 — Host UI Improvements

Host UI should eventually provide:

```text
Lab: Lab 2.2.1

Connected:
28 / 30

Computer        IP             Status       UWF
---------------------------------------------------
Komputer-01     10.22.4.1      Online       Locked
Komputer-02     10.22.4.2      Online       Locked
...
Komputer-30     10.22.4.30     Offline      Unknown
```

Host should derive expected computer identity from actual Windows hostname.

Do not require manual setup for every Client.

---

# Phase 14 — Configuration

## Status

DONE.

Host derives Client targets from its own `10.x.x.90` interface and scans
`.1` through `.89`. It also scans `127.0.0.1` for a one-PC development test.

Host configuration should eventually support:

```text
Lab Name
```

Persist configuration locally.

Example:

```json
{
  "labName": "Lab 2.2.1"
}
```

Client should require minimal/no manual configuration.

TCP port remains fixed at `5020`; Client requires no local configuration.

---

# Phase 15 — Deployment

Final deployment target:

```text
Windows x64
Self-contained
Single-file
No .NET Runtime installation required
```

Current publish policy:

```text
Self-contained       ON
Single-file          ON
win-x64              ON
PublishTrimmed       OFF
PublishAot           OFF
ReadyToRun            OFF
```

Do not enable trimming/AOT merely to reduce file size without testing.

---

# Testing Strategy

## Unit / Regression

Maintain lifecycle tests covering:

- register
- heartbeat
- stale timeout
- disconnect
- reconnect
- old connection heartbeat
- duplicate hostname
- malformed message
- EOF

## Integration

Test:

```text
1 Host + 1 Client
1 Host + 5 Clients
1 Host + 10 Clients
1 Host + 20 Clients
1 Host + 30 Clients
```

Verify:

- connection stability
- heartbeat stability
- UI responsiveness
- memory usage
- CPU usage
- reconnect behavior

## Real Lab Test

Before production:

1. Deploy Host to real lab Host PC.
2. Deploy Client to several student PCs.
3. Verify automatic Host discovery.
4. Verify Online/Offline.
5. Test multiple simultaneous Clients.
6. Test Host restart.
7. Test Client restart.
8. Test network interruption.
9. Test UWF status.
10. Test UWF lock/unlock.
11. Test batch operations.

---

# Important Constraints

Do NOT:

- add unnecessary dependencies
- introduce a database for Client state
- scan addresses outside the established `.1` through `.89` Client range
- create a new TCP connection for every command
- execute arbitrary commands received over the network
- require manual configuration on every student PC
- implement UWF before the command protocol is stable
- break the existing heartbeat lifecycle tests

Prefer:

- standard .NET libraries
- TCP
- newline-delimited JSON
- async/await
- immutable/explicit message contracts where practical
- simple architecture
- low CPU/memory overhead
- graceful reconnect
- structured logging

---

# Immediate Next Task

1. Validate Host `.90` scanning and Client inbound firewall behavior in a real lab.
2. Validate UWF status, lock, and unlock on the production Windows image.
3. Add production file logging plus optional service uninstall/update scripts.
4. Decide whether the accepted unauthenticated-transport risk remains suitable
   before deployment outside the controlled lab network.
