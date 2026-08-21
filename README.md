# Deep Fry — LabManagement

Deep Fry adalah aplikasi internal UPT Lab Universitas Amikom Yogyakarta untuk
memantau dan mengendalikan UWF pada PC laboratorium Windows.

Sistem terdiri dari:

- `LabManagement.Host`: dashboard WPF pada PC staff dengan alamat lab `.90`.
- `LabManagement.Client`: listener ringan pada setiap PC mahasiswa.
- `LabManagement.Protocol`: kontrak JSON newline-delimited yang dipakai keduanya.

## Topologi jaringan

Client membuka TCP port `5020`. Host mencari Client pada alamat `.1` sampai
`.89` di subnet dari interface Host `10.x.x.90`, kemudian Host membuat koneksi
ke Client yang ditemukan.

```text
Host 10.22.4.90                       Client 10.22.4.1–89
┌─────────────────────┐   TCP 5020   ┌──────────────────────┐
│ scan subnet         │ ───────────> │ listener             │
│ daftar Online       │              │ REGISTER + HEARTBEAT │
│ command UWF         │ <──────────> │ allowlisted UWF      │
└─────────────────────┘              └──────────────────────┘
```

Host juga memindai `127.0.0.1` agar Host dan Client dapat diuji pada satu PC.
Identitas Client tetap berasal dari `Environment.MachineName`; tidak ada file
nama komputer atau pairing key yang perlu dikonfigurasi.

## Fitur saat ini

- Login password lokal untuk aplikasi Host.
- Penemuan otomatis Client berdasarkan konvensi IP lab.
- REGISTER, heartbeat dua detik, timeout offline enam detik, dan reconnect.
- Daftar hostname, IP, koneksi, status UWF, dan hasil command.
- Seleksi satu/banyak/semua Client.
- `Refresh UWF Status`, `Lock Selected`, dan `Unlock Selected`.
- Client dapat dijalankan langsung atau dipasang sebagai Windows Service.
- Firewall Client TCP 5020 dibatasi ke `LocalSubnet`.

## Batasan keamanan

Pairing key sengaja tidak digunakan. Karena itu sistem hanya boleh digunakan
pada jaringan lab internal yang terkontrol. Siapa pun yang dapat menjangkau TCP
5020 Client berpotensi meniru Host. Client tetap menolak command di luar
allowlist, tetapi koneksi ini tidak memakai autentikasi maupun enkripsi.

Password Host hanya melindungi dashboard. Administrator lokal Windows tetap
dapat mengubah atau menghentikan aplikasi.

## Kebutuhan development

- Windows 10/11 x64.
- .NET SDK 8.
- Visual Studio dengan workload .NET Desktop Development, atau .NET CLI.
- Hak Administrator untuk menjalankan Client/UWF dan membuat firewall rule.

## Build

```powershell
dotnet restore LabManagement.sln
dotnet build LabManagement.sln
```

Build ke folder verifikasi terpisah berguna ketika EXE Debug sedang berjalan:

```powershell
dotnet build LabManagement.sln `
  -p:OutputPath="$PWD\.verify\solution\" `
  -p:UseAppHost=false
```

## Test

```powershell
dotnet build tests\LabManagement.Host.LifecycleTests\LabManagement.Host.LifecycleTests.csproj `
  -p:OutputPath="$PWD\.verify\tests\" `
  -p:UseAppHost=false

dotnet .\.verify\tests\LabManagement.Host.LifecycleTests.dll
dotnet .\.verify\tests\LabManagement.Host.LifecycleTests.dll --ui-startup
dotnet .\.verify\tests\LabManagement.Host.LifecycleTests.dll --ui-layout
```

Test jaringan mencakup arah koneksi baru: Host mencari listener Client, menerima
REGISTER/HEARTBEAT, dan mengirim command dengan response yang sesuai request ID.

## Test satu PC

1. Jalankan `LabManagement.Client` dari Visual Studio atau EXE publish.
2. Izinkan UAC dan Windows Firewall bila diminta.
3. Jalankan `LabManagement.Host`.
4. Buat/login password Host.
5. Client akan ditemukan melalui `127.0.0.1` dalam beberapa detik.

Tidak diperlukan `.env`, pairing key, atau argumen command-line.

## Publish Release

Target: Windows x64, self-contained, single-file, trimming/AOT/ReadyToRun OFF.

### Host

```powershell
dotnet publish LabManagement.Host\LabManagement.Host.csproj `
  -c Release -r win-x64 --self-contained true `
  -o .\publish\Host `
  -p:PublishSingleFile=true `
  -p:PublishTrimmed=false `
  -p:PublishAot=false `
  -p:PublishReadyToRun=false
```

### Client

```powershell
dotnet publish LabManagement.Client\LabManagement.Client.csproj `
  -c Release -r win-x64 --self-contained true `
  -o .\publish\Client `
  -p:PublishSingleFile=true `
  -p:PublishTrimmed=false `
  -p:PublishAot=false `
  -p:PublishReadyToRun=false
```

Salin `scripts\Install-LabManagementClient.ps1` ke paket Client jika mode
Windows Service akan digunakan.

## Penggunaan di lab

### Host

1. Letakkan Host pada PC dengan IPv4 `10.x.x.90`.
2. Jalankan `LabManagement.Host.exe`.
3. Buat password pada penggunaan pertama.
4. Isi nama ruangan melalui **Lab Settings**.
5. Host otomatis memindai `.1` sampai `.89` setiap beberapa detik.

Host tidak lagi membutuhkan inbound firewall TCP 5020 karena koneksi dimulai
oleh Host menuju Client.

### Client langsung

1. Ekstrak paket Client ke PC mahasiswa.
2. Jalankan `LabManagement.Client.exe` sebagai Administrator.
3. Terima prompt UAC. Firewall rule TCP 5020 untuk `LocalSubnet` dibuat otomatis.
4. Biarkan aplikasi berjalan selama PC ingin dikelola dari Host.

Menutup Client membuat PC tersebut menjadi Offline di Host.

### Client sebagai service opsional

Buka PowerShell Administrator:

```powershell
.\Install-LabManagementClient.ps1 `
  -ExecutablePath "C:\LabManagement\LabManagement.Client.exe" `
  -Start
```

Periksa service:

```powershell
Get-Service -Name "LabManagement Client"
Restart-Service -Name "LabManagement Client"
```

## Troubleshooting

Dari Host, pastikan Client dapat dijangkau:

```powershell
Test-NetConnection 10.22.4.13 -Port 5020
```

Jika gagal:

- pastikan Client sedang berjalan sebagai Administrator;
- pastikan IP Host berakhiran `.90` dan keduanya berada pada subnet yang sama;
- periksa firewall rule `Deep Fry Client TCP 5020` pada Client;
- pastikan tidak ada aplikasi lain memakai TCP 5020;
- pastikan network profile dan switch lab mengizinkan komunikasi antarkomputer.

UWF wajib diuji pada image Windows lab sebenarnya sebelum operasi massal.

---

© 2025 Gusti Padaka — NIM 22.11.5020 — Universitas Amikom Yogyakarta
