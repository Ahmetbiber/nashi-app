import AppKit
import SwiftUI
import NashiCore

struct ToolSetupView:View {
    var onWorkingChange:(Bool)->Void = {_ in}
    @State private var working=false
    @State private var fraction:Double?
    @State private var message="Choose the tools you need. Files are downloaded directly from their publishers and checked before installation."
    @State private var mediaReady=ExternalToolSetup.mediaReady
    @State private var officeReady=ExternalToolSetup.officeReady
    @State private var task:Task<Void,Never>?
    @State private var token=CancellationToken()
    var body:some View {
        VStack(alignment:.leading,spacing:20) {
            NashiPageHeading(eyebrow:"FIRST-TIME SETUP",title:"A few tools. More possibilities.",subtitle:"One download per tool group. No account or GitHub token.")
            GroupBox {
                VStack(alignment:.leading,spacing:12) {
                    Label("Media tools",systemImage:"film").font(.headline)
                    Text("FFmpeg, ffprobe, yt-dlp and Deno enable media conversion, previews and web downloads. Approximately 180 MB. FFmpeg and yt-dlp use GPL licenses; Deno uses MIT.").font(.callout).foregroundStyle(.secondary)
                    Button(mediaReady ? "Media tools ready":"Download media tools") {install(office:false)}.disabled(working || mediaReady)
                }.padding(10).frame(maxWidth:.infinity,alignment:.leading)
            }
            GroupBox {
                VStack(alignment:.leading,spacing:12) {
                    Label("Office tools",systemImage:"doc.text").font(.headline)
                    Text("LibreOffice 26.8.1 enables Office document conversions. A 285 MB download; about 1 GB installed. MPL 2.0 and its included third-party licenses.").font(.callout).foregroundStyle(.secondary)
                    Button(officeReady ? "Office tools ready":"Download office tools") {install(office:true)}.disabled(working || officeReady)
                }.padding(10).frame(maxWidth:.infinity,alignment:.leading)
            }
            LText(message).font(.callout).foregroundStyle(.secondary).fixedSize(horizontal:false,vertical:true)
            if working {
                if let fraction {ProgressView(value:fraction)}else{ProgressView().controlSize(.small)}
                Button("Cancel setup") {token.cancel();task?.cancel()}
            }
            Link("Upstream versions, licenses and source links",destination:URL(string:"https://github.com/orkunisitmak/nashi-app/blob/main/docs/dependencies.md")!).font(.callout)
        }.padding(28).frame(width:590).background(NashiPalette.canvas).tint(.nashi).buttonStyle(NashiButtonStyle())
    }
    private func install(office:Bool) {
        working=true;onWorkingChange(true);fraction=nil;token=CancellationToken();let currentToken=token
        task=Task {
            do {
                let report:@Sendable(Double?,String)->Void = { value,label in Task{@MainActor in fraction=value;message=label} }
                if office {try await ExternalToolSetup.installOffice(token:currentToken,progress:report)}else{try await ExternalToolSetup.installMedia(token:currentToken,progress:report)}
                mediaReady=ExternalToolSetup.mediaReady;officeReady=ExternalToolSetup.officeReady;message="Ready. You can go back to your files."
            }catch {message=currentToken.isCancelled ? "Setup cancelled. Existing tools were kept.":error.localizedDescription}
            working=false;onWorkingChange(false)
        }
    }
}
extension Notification.Name {static let showToolSetup=Notification.Name("NashiShowToolSetup")}
