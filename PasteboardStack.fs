namespace CopyButler

open System
open System.Collections.Generic

open AppKit
open Foundation
open ObjCRuntime

type PasteboardItems = INSPasteboardWriting array

type PasteboardProvider(data, onPaste) =
    inherit NSPasteboardItemDataProvider()
    override __.ProvideDataForType(_, item, _) =
        for (d, t) in data do
            item.SetDataForType(d, t) |> ignore
        onPaste()
    override __.FinishedWithDataProvider(_) = ()

type PasteboardStack(pb : NSPasteboard) =

    let mutable lastChange = nint 0
    let stack = Stack<PasteboardItems>()

    // Sets an item to the pasteboard without causing it
    //  to be pushed onto the stack..
    let setPb (items : PasteboardItems) =
        pb.ClearContents() |> ignore
        pb.WriteObjects(items) |> ignore
        lastChange <- pb.ChangeCount

    // Pops an item from the stack (if there is one) and sets it to the pasteboard
    let popAndSet() =
        if stack.Count > 0 then
            stack.Pop() |> setPb

    let onPaste() =
        if stack.Count > 0 then
            // Pop the item that was just pasted
            stack.Pop() |> ignore
        // Set the next item to the pasteboard
        //  We need to defer this, otherwise it screws up
        //  the current paste operation..
        NSTimer.CreateScheduledTimer(0.1, fun _ -> popAndSet()) |> ignore

    let makeItem data =
        let item = new NSPasteboardItem()
        let provider = new PasteboardProvider(data, onPaste)
        item.SetDataProviderForTypes(provider, data |> Array.map (fun (_, t) -> t)) |> ignore
        item :> INSPasteboardWriting

    member __.CheckAndPushIfNecessary() =
        if pb.ChangeCount <> lastChange then
            lastChange <- pb.ChangeCount
            // Take the new item and replace it with a surrogate that will notify us
            //  when it's pasted..
            match pb.PasteboardItems with
            | null  -> ()
            | items ->
                let datas = items
                            |> Array.choose (fun oldItem ->
                                match oldItem.Types with
                                | null  -> None
                                | types ->
                                    types
                                    |> Array.choose (fun t -> 
                                        match oldItem.GetDataForType(t) with
                                        | null -> None
                                        | d    -> Some (d.MutableCopy() :?> NSData, t)
                                    )
                                    |> Some
                            )
                // Unfortunately, we need to create 2 separate NSPasteboardItems,
                //  one to set back to the pasteboard immediately, and one to push on the stack.
                datas |> Array.map makeItem |> setPb
                datas |> Array.map makeItem |> stack.Push |> ignore

    member __.Push([<ParamArrayAttribute>] items : PasteboardItems) = stack.Push(items)
    member __.Pop() = popAndSet()
    member __.Clear() = stack.Clear()
