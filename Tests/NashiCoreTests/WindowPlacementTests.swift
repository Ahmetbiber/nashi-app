import XCTest
@testable import NashiCore

final class WindowPlacementTests: XCTestCase {
    func testSevenRequestedPlacementsPartitionOddAndRetinaScreens() {
        XCTAssertEqual(WindowPlacement.allCases.count, 7)
        for area in [CGRect(x:0,y:24,width:1511,height:910),
                     CGRect(x:-1920,y:-1030,width:1920,height:1006),
                     CGRect(x:1512,y:40,width:1727.5,height:1090)] {
            for scale:CGFloat in [1,2] {
                let left=WindowPlacement.leftThird.frame(in:area,scale:scale)
                let middle=WindowPlacement.middleThird.frame(in:area,scale:scale)
                let right=WindowPlacement.rightThird.frame(in:area,scale:scale)
                XCTAssertEqual(left.minX,area.minX)
                XCTAssertEqual(left.maxX,middle.minX)
                XCTAssertEqual(middle.maxX,right.minX)
                XCTAssertEqual(right.maxX,area.maxX,accuracy:0.5/scale)
                XCTAssertEqual(WindowPlacement.leftTwoThirds.frame(in:area,scale:scale).maxX,middle.maxX)
                XCTAssertEqual(WindowPlacement.rightTwoThirds.frame(in:area,scale:scale).minX,middle.minX)
                XCTAssertEqual(WindowPlacement.leftHalf.frame(in:area,scale:scale).maxX,
                               WindowPlacement.rightHalf.frame(in:area,scale:scale).minX)
                for placement in WindowPlacement.allCases {
                    let frame=placement.frame(in:area,scale:scale)
                    XCTAssertEqual(frame.minY,area.minY)
                    XCTAssertEqual(frame.height,area.height)
                    XCTAssertGreaterThan(frame.width,0)
                }
            }
        }
    }

    func testContentDragsAndFreshFilePayloadNeverActivateWindowWheel() {
        var intent=WindowDragIntent()
        XCTAssertFalse(intent.accepts(pointerDistance:200,frameChanged:false,freshFileDrag:false,requested:true))
        XCTAssertFalse(intent.accepts(pointerDistance:2,frameChanged:true,freshFileDrag:false,requested:true))
        XCTAssertFalse(intent.accepts(pointerDistance:80,frameChanged:true,freshFileDrag:false,requested:false))
        XCTAssertTrue(intent.accepts(pointerDistance:80,frameChanged:true,freshFileDrag:false,requested:true))
        XCTAssertFalse(intent.accepts(pointerDistance:80,frameChanged:true,freshFileDrag:true,requested:true))
        XCTAssertFalse(intent.accepts(pointerDistance:80,frameChanged:true,freshFileDrag:false,requested:true))
    }

    func testEscapeCancellationLastsUntilTheNextDrag() {
        var intent=WindowDragIntent();intent.cancel()
        XCTAssertFalse(intent.accepts(pointerDistance:80,frameChanged:true,freshFileDrag:false,requested:true))
        var next=WindowDragIntent()
        XCTAssertTrue(next.accepts(pointerDistance:80,frameChanged:true,freshFileDrag:false,requested:true))
    }
}
