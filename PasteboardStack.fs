namespace CopyButler

open System
open System.Collections.Generic

open AppKit
open Foundation
open ObjCRuntime

type PasteboardStack(pb : NSPasteboard) =

    let stack = Stack<_>()
    let mutable lastChange = nint 0

    let rec onPaste() =
        // Pop the item that was just pasted (don't pop the last item)
        if stack.Count > 1 then
            stack.Pop() |> ignore
            // Set the next item to the pasteboard
            //  We need to defer this, otherwise it screws up
            //  the current paste operation..
            NSTimer.CreateScheduledTimer(0.1, fun _ ->
                if stack.Count > 0 then
                    stack.Peek() |> setPb
            )
            |> ignore

    // Sets an item to the pasteboard without causing it
    //  to be pushed onto the stack..
    and setPb data =
        let items : INSPasteboardWriting array = Array.map makeItem data
        pb.ClearContents() |> ignore
        pb.WriteObjects(items) |> ignore
        lastChange <- pb.ChangeCount

    and makeItem data =
        let item = new NSPasteboardItem()
        let provider = {
            new NSPasteboardItemDataProvider() with
                override __.FinishedWithDataProvider(_) = ()
                override __.ProvideDataForType(_, item, _) =
                    for (d, t) in data do
                        item.SetDataForType(d, t) |> ignore
                    onPaste()
        }
        item.SetDataProviderForTypes(provider, Array.map snd data) |> ignore
        item :> INSPasteboardWriting

    member __.Clear() = stack.Clear()
    member __.CheckAndPushIfNecessary() =
        if pb.ChangeCount <> lastChange then
            lastChange <- pb.ChangeCount
            // Take the new item and replace it with a surrogate that will notify us
            //  when it's pasted..
            match pb.PasteboardItems with
            | null  -> ()
            | items ->
                let data = items
                           |> Array.choose (fun oldItem ->
                               match oldItem.Types with
                               | null  -> None
                               | types ->
                                   types
                                   |> Array.choose (fun t ->
                                       match oldItem.GetDataForType(t) with
                                       | null -> None
                                       | d    -> Some (d, t)
                                   )
                                   |> Some
                           )
                setPb data
                stack.Push(data)
