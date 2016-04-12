open System

open AppKit
open Foundation
open ObjCRuntime

open CopyButler

NSApplication.Init ()

[<Literal>]
let AppName = "CopyButler"
let App = NSApplication.SharedApplication

let toggleMenuItem (mi : NSMenuItem) =
    match mi.State with
    | NSCellStateValue.On ->
        mi.State <- NSCellStateValue.Off
        false
    | NSCellStateValue.Off ->
        mi.State <- NSCellStateValue.On
        true
    | _ -> failwith "Unexpected NSCellStateValue"

let pasteboardStack = PasteboardStack(NSPasteboard.GeneralPasteboard)
let mutable pasteboardTimer : NSTimer option = None

let enablePasteboardTimer enabled =
    match enabled, pasteboardTimer with
    | true, None ->
        pasteboardTimer <- NSTimer.CreateRepeatingScheduledTimer(0.3, fun _ -> pasteboardStack.CheckAndPushIfNecessary()) |> Some
    | false, Some t ->
        t.Invalidate()
        pasteboardTimer <- None
    | _, _ -> ()

type AppDelegate() =
    inherit NSApplicationDelegate()
    let mutable item : NSStatusItem = null
    override this.DidFinishLaunching(_) =
        item <- NSStatusBar.SystemStatusBar.CreateStatusItem(NSStatusItemLength.Square)
        item.Button.Title <- "🍻"
        
        let menu = new NSMenu()
        menu.AddItem <| new NSMenuItem("Record Copied Items", fun s e -> s :?> _ |> toggleMenuItem |> enablePasteboardTimer)
        menu.AddItem <| new NSMenuItem("Clear Copy Stack", fun s e -> pasteboardStack.Clear())
        menu.AddItem <| NSMenuItem.SeparatorItem
        menu.AddItem <| new NSMenuItem("Quit " + AppName, fun s e -> App.Terminate(this))
        item.Menu <- menu

App.Delegate <- new AppDelegate() :> INSApplicationDelegate
NSApplication.Main(Environment.GetCommandLineArgs())
