# U-MOCO FreeD Repeater

**U-MOCO FreeD Repeater** is a Windows desktop application (WPF, .NET 10) built for camera tracking workflows in broadcast, film, and virtual production environments.

## Overview

It receives **FreeD protocol** UDP packets — an industry-standard protocol used by camera tracking systems to transmit real-time camera position and orientation data — and provides **real-time monitoring, visualization, and forwarding** of that data to one or more downstream targets simultaneously.

## Key Features

### 📡 Reception

- **UDP Listener** — Binds to a configurable local port to receive FreeD protocol packets from any compatible camera tracking system (e.g., U-MOCO).
- **Packet Rate Monitor** — Continuously displays the incoming data frequency in Hz to verify signal integrity.
- **Timestamped Data Log** — Maintains a scrollable live table of up to 2,000 recent records, capturing all tracking parameters with millisecond-level timestamps.

### 📊 Monitoring & Visualization

- **Real-time Chart** — Plots live waveforms for all 8 tracking channels: **X, Y, Z** (position), **Pan, Tilt, Roll** (orientation), **Focus**, and **Zoom**.
- **Channel Selection** — Quickly switch between grouped views (XYZ / Pan-Tilt-Roll / Focus-Zoom) or inspect any individual channel.
- **Ping Monitor** — Continuously pings each configured forward target and displays network latency with color-coded indicators (🟢 ≤3 ms / 🟡 ≤15 ms / 🔴 >15 ms).

### 🔁 Forwarding

- **Multi-target UDP Forwarding** — Forwards raw FreeD packets to multiple IP/port destinations in real time, enabling data distribution to several rendering engines or applications simultaneously.

### 💾 Data & Configuration

- **CSV Export** — Exports the recorded tracking data to a CSV file for offline review and analysis.
- **Persistent Configuration** — Automatically saves and restores the listen port and all forward target settings between sessions.

## Typical Use Case

A camera tracking device (such as a U-MOCO motion control head) sends FreeD UDP packets to this application. The operator can **monitor all tracking data in real time** — verifying signal quality, inspecting individual axis movements, and checking network health — while the application simultaneously **rebroadcasts** those packets to multiple consumers such as Unreal Engine, disguise, or other virtual production software.

## Requirements

- Windows 10 / 11
- [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)

## Related Links

- 🌐 Website: [www.u-moco.com](https://www.u-moco.com)
- 💻 GitHub: [machens/U-MOCO-FreeD-Repeater](https://github.com/machens/U-MOCO-FreeD-Repeater)