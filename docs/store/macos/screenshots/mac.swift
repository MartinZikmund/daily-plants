import AppKit
import ApplicationServices
import CoreGraphics

// Small helpers capture-app.sh drives the app with. Positions are in points, relative to the window's top-left corner.
//   mode                      prints the main display's mode as "<width> <height> <hidpi 0|1>"
//   mode <w> <h> <hidpi>      switches the main display to that mode, for this login session only
//   frame <w> <h>             moves the app's window to the top left and sizes it, title bar included
//   click <x> <y>             clicks at that point in the window
//   scroll <x> <y> <lines>    scrolls the wheel over that point, negative lines scroll down
//   capture <file>            saves the window, without its shadow, at the display's pixel density
let args = Array(CommandLine.arguments.dropFirst())

func fail(_ message: String) -> Never {
    FileHandle.standardError.write((message + "\n").data(using: .utf8)!)
    exit(1)
}

func appWindow() -> (element: AXUIElement, pid: pid_t) {
    guard let app = NSWorkspace.shared.runningApplications.first(where: { $0.executableURL?.lastPathComponent == "DailyPlants" }) else {
        fail("Daily Plants isn't running.")
    }
    let root = AXUIElementCreateApplication(app.processIdentifier)
    var windows: CFTypeRef?
    guard AXUIElementCopyAttributeValue(root, kAXWindowsAttribute as CFString, &windows) == .success,
          let window = (windows as? [AXUIElement])?.first else {
        fail("Daily Plants has no window.")
    }
    return (window, app.processIdentifier)
}

func origin(of window: AXUIElement) -> CGPoint {
    var value: CFTypeRef?
    var point = CGPoint.zero
    AXUIElementCopyAttributeValue(window, kAXPositionAttribute as CFString, &value)
    AXValueGetValue(value as! AXValue, .cgPoint, &point)
    return point
}

func activate(_ pid: pid_t) {
    NSRunningApplication(processIdentifier: pid)?.activate()
    usleep(300_000)
}

func post(_ type: CGEventType, at point: CGPoint) {
    CGEvent(mouseEventSource: nil, mouseType: type, mouseCursorPosition: point, mouseButton: .left)?.post(tap: .cghidEventTap)
}

switch args.first {
case "mode":
    let display = CGMainDisplayID()
    let current = CGDisplayCopyDisplayMode(display)!
    if args.count == 1 {
        print("\(current.width) \(current.height) \(current.pixelWidth > current.width ? 1 : 0)")
        break
    }
    guard args.count == 4, let width = Int(args[1]), let height = Int(args[2]) else { fail("mode <w> <h> <hidpi>") }
    let hidpi = args[3] == "1"
    let options = [kCGDisplayShowDuplicateLowResolutionModes: kCFBooleanTrue] as CFDictionary
    let modes = (CGDisplayCopyAllDisplayModes(display, options) as! [CGDisplayMode])
        .filter { $0.isUsableForDesktopGUI() && $0.width == width && $0.height == height && ($0.pixelWidth > $0.width) == hidpi }
    guard let mode = modes.first(where: { abs($0.refreshRate - current.refreshRate) < 1 }) ?? modes.first else {
        fail("The display has no \(width)x\(height) mode\(hidpi ? " in HiDPI" : "").")
    }
    var config: CGDisplayConfigRef?
    CGBeginDisplayConfiguration(&config)
    CGConfigureDisplayWithDisplayMode(config, display, mode, nil)
    guard CGCompleteDisplayConfiguration(config, .forSession) == .success else { fail("Switching the display mode failed.") }
case "frame":
    guard args.count == 3, var size = CGSize?(CGSize(width: Double(args[1])!, height: Double(args[2])!)) else { fail("frame <w> <h>") }
    let (window, pid) = appWindow()
    activate(pid)
    var position = CGPoint(x: 40, y: 40)
    AXUIElementSetAttributeValue(window, kAXPositionAttribute as CFString, AXValueCreate(.cgPoint, &position)!)
    AXUIElementSetAttributeValue(window, kAXSizeAttribute as CFString, AXValueCreate(.cgSize, &size)!)
case "click":
    guard args.count == 3, let x = Double(args[1]), let y = Double(args[2]) else { fail("click <x> <y>") }
    let (window, pid) = appWindow()
    activate(pid)
    let o = origin(of: window)
    let point = CGPoint(x: o.x + x, y: o.y + y)
    post(.mouseMoved, at: point)
    usleep(150_000)
    post(.leftMouseDown, at: point)
    usleep(60_000)
    post(.leftMouseUp, at: point)
case "scroll":
    guard args.count == 4, let x = Double(args[1]), let y = Double(args[2]), let lines = Int32(args[3]) else { fail("scroll <x> <y> <lines>") }
    let (window, pid) = appWindow()
    activate(pid)
    let o = origin(of: window)
    post(.mouseMoved, at: CGPoint(x: o.x + x, y: o.y + y))
    usleep(150_000)
    let step: Int32 = lines < 0 ? -1 : 1
    for _ in 0..<abs(lines) {
        CGEvent(scrollWheelEvent2Source: nil, units: .line, wheelCount: 1, wheel1: step, wheel2: 0, wheel3: 0)?.post(tap: .cghidEventTap)
        usleep(30_000)
    }
case "capture":
    guard args.count == 2 else { fail("capture <file>") }
    let (_, pid) = appWindow()
    let info = CGWindowListCopyWindowInfo([.optionOnScreenOnly, .excludeDesktopElements], kCGNullWindowID) as! [[String: Any]]
    guard let id = info.first(where: { ($0[kCGWindowOwnerPID as String] as? pid_t) == pid && ($0[kCGWindowLayer as String] as? Int) == 0 })?[kCGWindowNumber as String] as? Int else {
        fail("Couldn't find the window to capture.")
    }
    let task = Process()
    task.executableURL = URL(fileURLWithPath: "/usr/sbin/screencapture")
    task.arguments = ["-x", "-o", "-l", String(id), args[1]]
    try! task.run()
    task.waitUntilExit()
    exit(task.terminationStatus)
default:
    fail("Usage: mac <mode|frame|click|scroll|capture> ...")
}
