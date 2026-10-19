import Foundation

public struct WebPhoto:Identifiable,Sendable {
    public let id:Int
    public let url:URL
    public let width:Int?
    public let height:Int?
}

enum InstagramPhotos {
    static func parse(_ data:Data,url:URL) throws -> WebMediaInfo {
        guard WebMediaDragPayload.isInstagramPhotoPost(url),
              let root=try JSONSerialization.jsonObject(with:data) as? [String:Any] else {
            throw NashiError.failed("The post did not return readable photo details.")
        }
        let entries=(root["entries"] as? [[String:Any]]) ?? [root]
        guard !entries.isEmpty,entries.count<=50 else { throw NashiError.unsupported("Use one Instagram photo post with up to 50 items.") }
        var photos:[WebPhoto]=[],videos=0
        for (index,entry) in entries.enumerated() {
            let formats=entry["formats"] as? [[String:Any]] ?? []
            let hasVideo=formats.contains { ($0["url"] as? String) != nil } || (entry["duration"] as? Double ?? 0)>0
            if hasVideo { videos += 1;continue }
            let variants=(entry["thumbnails"] as? [[String:Any]] ?? []).compactMap { item -> (URL,Int?,Int?)? in
                guard let text=item["url"] as? String,let image=try? WebDownloadEngine.link(text),image.scheme=="https",
                      let host=image.host?.lowercased(),["instagram.com","cdninstagram.com","fbcdn.net"].contains(where:{host==$0 || host.hasSuffix("."+$0)}) else { return nil }
                func dimension(_ key:String)->Int? { (item[key] as? Int).flatMap{(1...16_384).contains($0) ? $0:nil} }
                return (image,dimension("width"),dimension("height"))
            }
            guard let best=variants.max(by:{($0.1 ?? 0)*($0.2 ?? 0)<($1.1 ?? 0)*($1.2 ?? 0)}) else {
                throw NashiError.failed("A photo in this post is unavailable. Try a fresh public post link.")
            }
            photos.append(WebPhoto(id:index+1,url:best.0,width:best.1,height:best.2))
        }
        guard !photos.isEmpty else { throw NashiError.unsupported("This post contains videos, not photos. Use its Reel link to download video.") }
        return WebMediaInfo(url:url,title:photos.count==1 ? "Instagram photo":"Instagram photos",kind:.image,site:"Instagram",duration:nil,heights:[],direct:false,filename:nil,byteCount:nil,photos:photos,skippedVideoCount:videos)
    }

    static func download(_ info:WebMediaInfo,selected:Set<Int>?,directory:URL,token:CancellationToken,progress:@escaping ProgressHandler) async throws -> [URL] {
        let ids=Set(info.photos.map(\.id)),selection=selected ?? ids
        guard !selection.isEmpty,selection.isSubset(of:ids) else { throw NashiError.invalidInput("Select at least one photo from this post.") }
        let photos=info.photos.filter{selection.contains($0.id)}
        let shortcode=info.url.path.split(separator:"/").last.map(String.init) ?? "photos"
        let album=directory.appendingPathComponent("Instagram \(shortcode)",isDirectory:true)
        try FileManager.default.createDirectory(at:album,withIntermediateDirectories:false)
        var outputs:[URL]=[]
        for (index,photo) in photos.enumerated() {
            try token.check();try Task.checkCancellation()
            let itemDirectory=directory.appendingPathComponent("item-\(photo.id)",isDirectory:true)
            try FileManager.default.createDirectory(at:itemDirectory,withIntermediateDirectories:false)
            let item=WebMediaInfo(url:photo.url,title:"Photo \(photo.id)",kind:.image,site:"Instagram",duration:nil,heights:[],direct:true,filename:nil,byteCount:nil)
            let raw=try await WebDownloadEngine.downloadDirect(item,mode:.original,directory:itemDirectory,token:token) { fraction,_ in
                progress(fraction.isFinite && fraction>=0 ? (Double(index)+min(1,fraction))/Double(photos.count):OperationProgress.indeterminate,"Photo \(index+1) of \(photos.count)…")
            }
            guard raw.count==1,let source=raw.first else { throw NashiError.failed("A photo could not be downloaded.") }
            let target=album.appendingPathComponent(String(format:"%02d",photo.id)).appendingPathExtension(source.pathExtension)
            try FileManager.default.moveItem(at:source,to:target);outputs.append(target)
        }
        return outputs
    }
}
