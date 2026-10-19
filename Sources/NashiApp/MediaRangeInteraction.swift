import AppKit
import SwiftUI
import NashiCore

/// One immutable mouse-down snapshot, measured in the stationary lane. Neither a
/// resized handle nor SwiftUI's next range value can rebase an ongoing drag.
struct MediaRangeDrag {
    enum Target:Equatable { case start(Int),end(Int),move(Int),seek,add }
    enum Change:Equatable { case range(Int,TimeRange),seek(Double),addition(TimeRange) }
    let target:Target
    let origin:Double
    let duration:Double
    let secondsPerPoint:Double
    let initial:TimeRange?

    static func begin(x:Double,duration:Double,width:Double,ranges:[TimeRange],allowsAdding:Bool)->Self? {
        guard duration.isFinite,duration>0,width.isFinite,width>0,x.isFinite else { return nil }
        let secondsPerPoint=duration/width
        var target:Target=allowsAdding ? .add:.seek,initial:TimeRange?
        // Match the painted order when ranges overlap. Handles get a forgiving
        // hit area, and overlapping handles choose the closest edge.
        for (index,range) in ranges.enumerated().reversed() where range.start.isFinite && range.end.isFinite {
            let left=range.start/secondsPerPoint,right=left+max(14,(range.end-range.start)/secondsPerPoint)
            let startCenter=left+6,endCenter=right-6
            let startHit=abs(x-startCenter)<=10,endHit=abs(x-endCenter)<=10
            if startHit || endHit {
                target=startHit && (!endHit || abs(x-startCenter)<=abs(x-endCenter)) ? .start(index):.end(index)
            } else if x>=left && x<=right { target = .move(index) }
            else { continue }
            initial=TimeRange(start:max(0,min(duration,range.start)),end:max(0,min(duration,range.end)))
            break
        }
        return Self(target:target,origin:x,duration:duration,secondsPerPoint:secondsPerPoint,initial:initial)
    }
    func change(at x:Double)->Change {
        let x=x.isFinite ? x:origin
        let delta=(x-origin)*secondsPerPoint,minimum=min(0.02,duration)
        func time(_ point:Double)->Double { max(0,min(duration,point*secondsPerPoint)) }
        switch target {
        case .start(let index):
            let range=initial!
            return .range(index,TimeRange(start:max(0,min(range.end-minimum,range.start+delta)),end:range.end))
        case .end(let index):
            let range=initial!
            return .range(index,TimeRange(start:range.start,end:min(duration,max(range.start+minimum,range.end+delta))))
        case .move(let index):
            let range=initial!,length=max(0,range.end-range.start)
            let start=max(0,min(duration-length,range.start+delta))
            return .range(index,TimeRange(start:start,end:start+length))
        case .seek:return .seek(time(x))
        case .add:return .addition(TimeRange(start:min(time(origin),time(x)),end:max(time(origin),time(x))))
        }
    }
}

/// The transparent overlay owns the entire mouse sequence. Visual clips have no
/// competing gestures; AppKit keeps dispatching drags/up even beyond the lane.
struct MediaRangeInteraction: NSViewRepresentable {
    let duration:Double
    let ranges:[TimeRange]
    let label:String
    let onSeek:(Double)->Void
    let onChange:(Int,TimeRange)->Void
    let onPending:(TimeRange?)->Void
    var onAdd:((TimeRange)->Void)?

    func makeNSView(context:Context)->LaneView {
        let view=LaneView();view.setAccessibilityElement(true);view.setAccessibilityRole(.group)
        view.setAccessibilityHelp("Drag either edge to resize the range, or drag its middle to move it.")
        return view
    }
    func updateNSView(_ view:LaneView,context:Context) {
        let changed=view.ranges != ranges || view.duration != duration
        view.duration=duration;view.ranges=ranges;view.onSeek=onSeek;view.onChange=onChange;view.onPending=onPending;view.onAdd=onAdd
        view.setAccessibilityLabel("\(label.isEmpty ? "Media range":label) timeline")
        if changed { view.window?.invalidateCursorRects(for:view) }
    }
    static func dismantleNSView(_ view:LaneView,coordinator:()) { view.cancel();view.onSeek=nil;view.onChange=nil;view.onPending=nil;view.onAdd=nil }

    final class LaneView:NSView {
        var duration=0.0
        var ranges:[TimeRange]=[]
        var onSeek:((Double)->Void)?
        var onChange:((Int,TimeRange)->Void)?
        var onPending:((TimeRange?)->Void)?
        var onAdd:((TimeRange)->Void)?
        private var drag:MediaRangeDrag?
        override var isFlipped:Bool { true }
        override var acceptsFirstResponder:Bool { true }
        override var mouseDownCanMoveWindow:Bool { false }
        override func acceptsFirstMouse(for event:NSEvent?)->Bool { true }
        override func mouseDown(with event:NSEvent) {
            window?.makeFirstResponder(self)
            drag=MediaRangeDrag.begin(x:Double(convert(event.locationInWindow,from:nil).x),duration:duration,width:Double(bounds.width),ranges:ranges,allowsAdding:onAdd != nil)
            update(event,ending:false)
        }
        override func mouseDragged(with event:NSEvent) { update(event,ending:false) }
        override func mouseUp(with event:NSEvent) { update(event,ending:true);cancel() }
        override func viewWillMove(toWindow newWindow:NSWindow?) { if newWindow==nil { cancel() };super.viewWillMove(toWindow:newWindow) }
        func cancel() { drag=nil;onPending?(nil) }
        private func update(_ event:NSEvent,ending:Bool) {
            guard let drag else { return }
            switch drag.change(at:Double(convert(event.locationInWindow,from:nil).x)) {
            case .range(let index,let range):if ranges.indices.contains(index) { onChange?(index,range) }
            case .seek(let time):onSeek?(time)
            case .addition(let range):
                if ending {
                    if range.end-range.start>0.01 { onAdd?(range) } else { onSeek?(range.end) }
                } else { onPending?(range) }
            }
        }
        override func resetCursorRects() {
            super.resetCursorRects()
            guard duration.isFinite,duration>0,bounds.width>0 else { return }
            if onAdd != nil { addCursorRect(bounds,cursor:.crosshair) }
            for range in ranges where range.start.isFinite && range.end.isFinite {
                let left=range.start/duration*Double(bounds.width),width=max(14,(range.end-range.start)/duration*Double(bounds.width))
                let body=NSRect(x:left,y:0,width:width,height:bounds.height).intersection(bounds)
                if !body.isEmpty { addCursorRect(body,cursor:.openHand) }
                for x in [left-4,left+width-16] {
                    let handle=NSRect(x:x,y:0,width:20,height:bounds.height).intersection(bounds)
                    if !handle.isEmpty { addCursorRect(handle,cursor:.resizeLeftRight) }
                }
            }
        }
    }
}
