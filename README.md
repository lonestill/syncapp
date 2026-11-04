# Device Ecosystem Prototype

Experimental local-network discovery and communication stack for Windows, Android, and .NET clients.

## Components

| Path | Purpose |
| --- | --- |
| `EcosystemCore/Ecosystem.Server` | .NET 8 TCP server on port `8080` |
| `EcosystemCore/Ecosystem.Contracts` | Shared plugin contract |
| `EcosystemWindows` | Windows WPF client with discovery and QR UI |
| `EcosystemAndroid` | Android/Kotlin client with Compose UI |
| `tgbot` | Early Telegram bot experiment |

## Protocol

The prototype sends a four-byte big-endian payload length followed by a UTF-8 payload of at most 10 MiB. The server replies with a framed acknowledgement.

There is currently no encryption, authentication, authorization, or compatibility guarantee. Do not expose the server to the public internet.

## Build and run

Requirements: .NET 8 SDK, Windows 10 1809+ for WPF, and Android Studio for the Android client.

```bash
dotnet run --project EcosystemCore/Ecosystem.Server/Ecosystem.Server.csproj
dotnet build EcosystemWindows/EcosystemWindows.csproj
```

Open `EcosystemAndroid` in Android Studio to build the mobile client.

## Status

Early prototype. The repository still contains generated artifacts and overlapping Gradle files from experimentation.

## License

No license has been declared.
