<div align="center">

# 📱 QR Attendance System
### Smart & Secure Local Attendance Management Solution

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![Windows](https://img.shields.io/badge/Platform-Windows-0078D6?style=for-the-badge&logo=windows&logoColor=white)](https://www.microsoft.com/windows/)
[![Language](https://img.shields.io/badge/Language-C%23-239120?style=for-the-badge&logo=c-sharp&logoColor=white)](https://docs.microsoft.com/en-us/dotnet/csharp/)
[![WPF](https://img.shields.io/badge/UI-WPF-blueviolet?style=for-the-badge&logo=windows)](https://docs.microsoft.com/en-us/dotnet/desktop/wpf/)
[![GitHub Stars](https://img.shields.io/github/stars/cluckegy/QR-Login?style=for-the-badge&color=yellow)](https://github.com/cluckegy/QR-Login)
[![Discord](https://img.shields.io/discord/1316492336340275330?color=5865F2&label=Discord&logo=discord&logoColor=white&style=for-the-badge)](https://discord.gg/2zeME6Ea)

</div>

---

## 📖 Overview

**QR Attendance System** is a robust, clean, and efficient desktop application designed to streamline the attendance tracking process for lectures and sessions. 

Built with **.NET 8 (WPF)**, it acts as a local server that generates unique dynamic sessions. Students can check in by scanning a QR code displayed on the instructor's screen, which directs them to a locally hosted captive portal—**no external internet required**. The system ensures security through device fingerprinting and IP validation to prevent proxy attendance.

## ✨ Key Features

- **🚀 Local HTTP Server**: Runs a lightweight web server directly from the application (no IIS needed).
- **🔒 Anti-Proxy Security**: 
  - Validates Local IP to ensure physical presence.
  - Device Fingerprinting to prevent multiple check-ins from the same device.
  - Camera blocking to ensure students stay on the page until lecture ends.
- **📱 Student Web Portal**:
  - Responsive, modern mobile-first interface.
  - Clean "White & Green" aesthetic.
  - Real-time session timer and status.
- **🎛️ Session Management**:
  - Adjustable Login and Lecture durations.
  - Ability to **Extend** session time on the fly.
  - Real-time monitoring of online visitors and checked-in students.
- **📊 Reporting**: Export attendance lists to CSV instantly.

## 🛠️ Technology Stack

- **Core**: C# .NET 8.0
- **Framework**: Windows Presentation Foundation (WPF)
- **Networking**: `System.Net.HttpListener` for handling local web requests.
- **Frontend (Student Portal)**: HTML5, CSS3 (Custom modern UI with Inter font).

### 📦 Libraries & Dependencies

| Library | Version | Usage |
|---------|---------|-------|
| **[QRCoder](https://github.com/codebude/QRCoder)** | `v1.7.0` | Generates high-quality QR codes for session links. |
| **System.Drawing.Common** | `v10.0.1` | Graphics processing for QR generation. |
| **MaterialDesignThemes** | `v5.1.0` | *Referenced for UI components.* |

## 🚀 Getting Started

1. **Run the Application**: Open `AttendanceSystem.exe` (Run as Admin is handled automatically).
2. **Start Session**: 
   - Choose Grade and Duration.
   - Click **Start Session**.
3. **Student Check-in**:
   - Students connect to the local network.
   - Scan the QR code.
   - Fill in Name/Phone on the web portal.
4. **Monitor**: Watch the live counter of checked-in students.
5. **Finish**: Stop the session and click **Export** to save the data.

## 👨‍💻 Developed By

**Code Luck**
- [GitHub Profile](https://github.com/cluckegy)
- [Discord Community](https://discord.gg/2zeME6Ea)

---

<div align="center">
  <sub>Built with ❤️ by Code Luck</sub>
</div>
