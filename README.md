# TpLinkKasa

Crestron SIMPL module for controlling TP-Link Kasa smart devices including outlets, dimmers, power strips, and smart bulbs. Provides device control, state monitoring, and energy management capabilities integrated with Crestron systems.

> **Note:**  
> All operations support thread-safe concurrent access patterns.  
> Real-time device state updates and comprehensive control feedback.

---

## ⚙️ Module Overview

The TpLinkKasa module suite provides comprehensive control and monitoring of TP-Link Kasa smart devices within Crestron environments with the following capabilities:

- **Smart Outlet Control** - On/off control and state monitoring for TP-Link Kasa outlets
- **Dimmer Support** - Brightness level control and dimmer state management
- **Power Strip Management** - Multi-outlet control and individual outlet state tracking
- **Smart Bulb Control** - Color and brightness adjustment for compatible Kasa bulbs
- **System Integration** - Central processor module for coordinating multiple Kasa devices
- **Real-Time Monitoring** - Device state feedback and status notifications
- **Thread-Safe Operations** - Concurrent device access with thread-safe implementations

---

## 🗂 Required Files

The module suite is available for use in Crestron systems:

* `TpLinkKasaOutlet.usp` - Outlet device control module (SIMPL+ source)
* `TpLinkKasaOutlet.ush` - Outlet device module header
* `TpLinkKasaDimmer.usp` - Dimmer device control module (SIMPL+ source)
* `TpLinkKasaDimmer.ush` - Dimmer device module header
* `TpLinkKasaPowerStrip.usp` - Power strip device control module (SIMPL+ source)
* `TpLinkKasaPowerStrip.ush` - Power strip device module header
* `TpLinkKasaSmartBulb.usp` - Smart bulb control module (SIMPL+ source)
* `TpLinkKasaSmartBulb.ush` - Smart bulb module header
* `TpLinkKasaSystemProcessor.usp` - System processor module (SIMPL+ source)
* `TpLinkKasaSystemProcessor.ush` - System processor module header
* `TpLinkKasa.clz` - Class library containing underlying device communication and management support
