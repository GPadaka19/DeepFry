# Deep Fry — LabManagement

Deep Fry adalah aplikasi manajemen PC laboratorium internal untuk UPT Lab
Universitas Amikom Yogyakarta. Sistem ini terdiri dari dua aplikasi Windows:

- **LabManagement.Host** — control center pada PC presentasi/dosen di setiap
  ruang lab. Dipakai staff UPT Lab.
- **LabManagement.Client** — background Client pada PC mahasiswa.

Client mengidentifikasi dirinya melalui hostname Windows dan mencari Host pada
alamat `.90` dalam subnet yang sama. Contoh: Client `10.22.1.13` akan mencari
Host di `10.22.1.90:5020`.

> Status: siap untuk pilot test terkontrol. Sebelum deployment massal, lakukan
> pengujian nyata UWF, Windows Service, reconnect setelah reboot, serta firewall
> pada lab target.

## Fitur saat ini

- REGISTER, heartbeat 2 detik, status Online/Offline, timeout 6 detik, dan
  reconnect aman.
- Monitoring PC berdasarkan hostname dan IP.
- Command dua arah dengan request ID, timeout, dan response terstruktur.
- `uwf.status`, `uwf.lock`, dan `uwf.unlock` melalui allowlist Client.
- Batch operation Host: Refresh UWF Status, Lock Selected, Unlock Selected.
- Password Host per lab, tersimpan sebagai PBKDF2 hash; password asli tidak
  disimpan.
- Client Pairing Key dengan challenge-response HMAC agar Client hanya menerima
  Host yang memiliki key lab yang benar.
- Client dapat dipasang sebagai Windows Service otomatis.
- Konfigurasi nama lab dan TCP port pada Host.

## Arsitektur singkat

```text
Host (.90, TCP 5020)  <--- koneksi outbound ---  Client mahasiswa
       |                                             |
       +-- dashboard, command, pairing key           +-- hostname Windows, UWF
```

Client tidak membuka port inbound. Setelah REGISTER, Host memberi challenge
acak; Client membalas HMAC menggunakan Client Pairing Key. Key asli tidak
dikirim melalui TCP.

## Prasyarat developer

- Windows x64
- .NET 8 SDK
- Visual Studio 2022/2026 dengan workload **.NET desktop development** untuk
  WPF Host
- PowerShell Administrator hanya untuk memasang Windows Service Client

## Struktur project

```text
LabManagement.Host/       WPF Host/control center
LabManagement.Client/     Worker Client dan UWF executor
LabManagement.Protocol/   Kontrak TCP dan framing JSON line
tests/                    Regression harness
scripts/                  Script instalasi Client
```

## Build untuk developer

```powershell
dotnet restore LabManagement.sln
dotnet build LabManagement.sln
```

Jika EXE Debug terkunci karena Host/Client sedang berjalan, hentikan debugging
lebih dulu. Alternatif build tanpa menyentuh `bin\Debug`:

```powershell
dotnet build LabManagement.sln `
  -p:OutputPath="$PWD\.verify\solution\" `
  -p:UseAppHost=false
```

## Menjalankan regression test

```powershell
dotnet build tests\LabManagement.Host.LifecycleTests\LabManagement.Host.LifecycleTests.csproj `
  -p:OutputPath="$PWD\.verify\tests\" `
  -p:UseAppHost=false

dotnet .\.verify\tests\LabManagement.Host.LifecycleTests.dll
```

Test UI tambahan:

```powershell
dotnet .\.verify\tests\LabManagement.Host.LifecycleTests.dll --ui-startup
dotnet .\.verify\tests\LabManagement.Host.LifecycleTests.dll --ui-layout
```

## Test satu PC (Host + Client)

1. Jalankan `LabManagement.Host` dengan F5 dari Visual Studio.
2. Buat/login password Host.
3. Klik **Client Pairing Key**, lalu salin key dari textbox.
4. Isi file lokal `LabManagement.Client\.env`:

   ```env
   LABMANAGEMENT_SHARED_SECRET=PASTE_KEY_DARI_HOST
   LABMANAGEMENT_HOST_IP=127.0.0.1
   ```

5. Rebuild lalu jalankan `LabManagement.Client` dengan F5.
6. Client seharusnya muncul Online di tabel Host dalam beberapa detik.

File `.env` diabaikan Git dan hanya untuk development. Jangan memasukkan key
produksi ke source control atau folder publish Release.

## Publish Release

Target publish adalah Windows x64, self-contained, dan single-file.

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

> Profil publish Visual Studio yang ada saat ini masih menyetel
> `PublishReadyToRun=true`. Gunakan command di atas sampai profil tersebut
> diselaraskan dengan kebijakan proyek: trimming, AOT, dan ReadyToRun nonaktif.

Salin `scripts\Install-LabManagementClient.ps1` ke paket Client sebelum
deployment.

## Setup Host untuk staff lab

1. Letakkan hasil publish Host pada PC `.90` ruang lab.
2. Jalankan `LabManagement.Host.exe`.
3. Saat pertama kali berjalan, buat password Host per lab.
4. Buka **Lab Settings** untuk mengatur nama lab dan port bila diperlukan.
   Port default adalah `5020`; perubahan port berlaku setelah Host dijalankan
   ulang.
5. Jika firewall belum mengizinkan koneksi, jalankan PowerShell Administrator:

   ```powershell
   New-NetFirewallRule `
     -DisplayName "LabManagement Host TCP 5020" `
     -Direction Inbound -Protocol TCP -LocalPort 5020 `
     -Action Allow -Profile Private
   ```

6. Klik **Client Pairing Key**. Key tersebut dibutuhkan ketika memasang Client
   di PC mahasiswa.

Password Host melindungi akses operasional aplikasi. Pada PC dengan akun Windows
bersama, administrator lokal tetap dapat mengubah file/aplikasi; ini bukan
proteksi terhadap administrator lokal.

## Setup Client sebagai Windows Service

Pada PC Client, copy hasil publish Client dan script instalasi ke lokasi tetap,
misalnya `C:\LabManagement`.

Buka PowerShell **Run as Administrator**, lalu jalankan:

```powershell
cd C:\LabManagement
.\Install-LabManagementClient.ps1 `
  -ExecutablePath "C:\LabManagement\LabManagement.Client.exe" `
  -SharedSecret "PAIRING_KEY_DARI_HOST" `
  -Start
```

Script akan:

- membuat service `LabManagement Client` dengan startup otomatis;
- menyimpan pairing key di
  `C:\ProgramData\LabManagement\Client\client-settings.json`;
- membatasi akses key untuk `SYSTEM` dan Administrators;
- mengatur Windows Service recovery untuk restart setelah kegagalan.

Perintah pemeriksaan:

```powershell
Get-Service -Name "LabManagement Client"
Restart-Service -Name "LabManagement Client"
```

Untuk pengujian teknis tanpa memasang service:

```powershell
.\LabManagement.Client.exe --console
```

Tanpa Host, Client hanya mencoba reconnect; ia tidak menjalankan aksi UWF.

## Operasional UWF untuk staff lab

- Gunakan **Refresh UWF Status** untuk membaca status aktual Client.
- Pilih beberapa PC dengan checkbox; Ctrl/Shift tetap dapat dipakai untuk
  seleksi baris.
- **Lock Selected** melindungi drive C; **Unlock Selected** membuka drive C.
- Perubahan konfigurasi UWF umumnya baru berlaku setelah restart Windows.
- Unlock hanya untuk maintenance/update oleh UPT Lab, bukan selama kegiatan
  pembelajaran biasa.

Jangan menganggap status koneksi sebagai status UWF. Status UWF harus diperoleh
dari response Client.

## Reset Client Pairing Key

Tombol **Reset Pairing Key** pada Host membuat key baru dan langsung memutus
semua Client aktif. Semua Client harus diperbarui memakai key baru sebelum bisa
Online kembali. Gunakan saat maintenance atau jika key diduga bocor.

## Checklist sebelum deployment massal

- [ ] Build Release Host dan Client sukses.
- [ ] Uji 1 Host + 1 Client dengan pairing key yang benar.
- [ ] Uji pairing key salah: Client harus ditolak.
- [ ] Uji service Client setelah reboot dan setelah Host restart.
- [ ] Uji firewall TCP 5020 pada Host.
- [ ] Uji `uwf.status`, lock, dan unlock pada PC yang benar-benar memiliki UWF.
- [ ] Uji batch operation pada beberapa Client.
- [ ] Pastikan `.env` dan pairing key development tidak ikut paket Release.

## Catatan keamanan dan batasan

- Sistem ini ditujukan untuk jaringan lab internal yang terkontrol.
- Tidak ada eksekusi shell command arbitrer dari Host; Client hanya menerima
  command dalam allowlist.
- UWF belum boleh dianggap tervalidasi untuk produksi sebelum diuji pada image
  Windows lab yang sesungguhnya.
- `LabManagement-Development-Plan.md` berisi roadmap dan status fase lebih
  rinci.
