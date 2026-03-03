# UsbSerialViewerUA

A lightweight Windows desktop app for viewing and monitoring USB storage device serial numbers. UI is in Ukrainian.

## Features

- Detects connected USB flash drives and external storage devices
- Displays device model name and serial number
- Real-time monitoring — auto-refreshes when USB devices are plugged in or removed
- Copy serial number to clipboard via right-click context menu
- Manual refresh button

## Requirements

- Windows 10 or later
- [.NET 9.0 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/9.0) (free)

## Free Software Needed

This application requires the **.NET 9.0 Desktop Runtime**, which is free to download and install from Microsoft:

1. Go to https://dotnet.microsoft.com/en-us/download/dotnet/9.0
2. Under **".NET Desktop Runtime"**, download the installer for your architecture (x64 or ARM64)
3. Run the installer

Alternatively, if you want to build from source, install the [.NET 9.0 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/9.0) (also free).

## Build & Run

```bash
dotnet build
dotnet run --project UsbSerialViewerUA
```

## Usage

1. Launch the application
2. Connected USB storage devices will appear automatically with their model and serial number
3. Right-click a device row to copy its serial number
4. Click **"Оновити"** (Refresh) to manually reload the device list
5. Plug in or remove a USB device — the list updates automatically

## Tech Stack

- C# / .NET 9.0
- Windows Forms
- WMI (System.Management) for USB device queries