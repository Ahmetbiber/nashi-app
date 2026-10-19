import AppKit
import QuickLookUI
import QuickLookThumbnailing
import UniformTypeIdentifiers

enum FloatShelfEdge:String,CaseIterable { case top,right,bottom,left }
enum FloatShelfViewMode:Int { case icons,list }

@MainActor final class FloatShelfController:NSWindowController,NSWindowDelegate,NSCollectionViewDataSource,NSCollectionViewDelegate,NSCollectionViewDelegateFlowLayout,@preconcurrency QLPreviewPanelDataSource,@preconcurrency QLPreviewPanelDelegate,NSMenuItemValidation {
    var onClose:(()->Void)?
    private(set) var isExpanded=true
    private(set) var viewMode:FloatShelfViewMode = .icons
    private(set) var attachedEdge:FloatShelfEdge?
    var urls:[URL] { items.map(\.url) }
    var selectedURLs:[URL] { selectedIndices.map{items[$0].url} }

    private var items:[ShelfFile]=[]
    private let surface=ShelfDropSurface()
    private let header=ShelfMoveHeader()
    private let collection=ShelfCollectionView()
    private let scroll=NSScrollView()
    private let flow=NSCollectionViewFlowLayout()
    private let countLabel=NSTextField(labelWithString:"")
    private let statusLabel=NSTextField(labelWithString:"")
    private let viewPicker=NSSegmentedControl(labels:["Icons","List"],trackingMode:.selectOne,target:nil,action:nil)
    private let collapseButton=NSButton()
    private let pinButton=NSButton()
    private let carryGrip=ShelfCarryGrip()
    private var headerHeight:NSLayoutConstraint!
    private var statusHeight:NSLayoutConstraint!
    private var expandedSize=NSSize(width:400,height:340)
    private var isAdjustingFrame=false
    private var previewItems:[ShelfPreviewItem]=[]
    private var sharingPicker:NSSharingServicePicker?
    private var typeBuffer=""
    private var lastTypeTime:TimeInterval=0
    private var isClosed=false
    private var selectedIndices:[Int] { collection.selectionIndexPaths.map(\.item).filter{items.indices.contains($0)}.sorted() }
    private var actionURLs:[URL] { selectedURLs.isEmpty ? urls:selectedURLs }

    init(urls:[URL]) {
        let panel=ShelfPanel(contentRect:NSRect(origin:.zero,size:expandedSize),styleMask:[.borderless,.resizable,.nonactivatingPanel],backing:.buffered,defer:false)
        super.init(window:panel)
        panel.title="Float";panel.level = .floating;panel.isFloatingPanel=true;panel.hidesOnDeactivate=false;panel.becomesKeyOnlyIfNeeded=false
        panel.collectionBehavior=[.canJoinAllSpaces,.fullScreenAuxiliary];panel.isReleasedWhenClosed=false;panel.isOpaque=false;panel.backgroundColor = .clear;panel.hasShadow=true;panel.minSize=NSSize(width:360,height:200)
        panel.delegate=self;panel.contentView=surface
        buildContent();append(urls);panel.center()
    }
    required init?(coder:NSCoder) { fatalError("init(coder:) has not been implemented") }
    override func showWindow(_ sender:Any?) {
        super.showWindow(sender);window?.makeKeyAndOrderFront(sender);window?.makeFirstResponder(collection)
    }

    func append(_ urls:[URL]) {
        let previousSelection=Set(selectedIndices.map{items[$0].id})
        var present=Set(items.map{$0.url.standardizedFileURL.path})
        for url in urls where url.isFileURL {
            let canonical=url.standardizedFileURL
            if present.insert(canonical.path).inserted { items.append(ShelfFile(url:canonical)) }
        }
        reload(preserving:previousSelection)
    }

    func attach(to edge:FloatShelfEdge?) {
        attachedEdge=edge
        if let edge,let window,let screen=window.screen ?? NSScreen.main {
            var frame=window.frame;let visible=screen.visibleFrame
            switch edge {
            case .left:frame.origin.x=visible.minX
            case .right:frame.origin.x=visible.maxX-frame.width
            case .top:frame.origin.y=visible.maxY-frame.height
            case .bottom:frame.origin.y=visible.minY
            }
            frame.origin.x=max(visible.minX,min(visible.maxX-frame.width,frame.minX));frame.origin.y=max(visible.minY,min(visible.maxY-frame.height,frame.minY))
            setFrame(frame,animate:true)
        }
        updateLabels()
    }

    private func buildContent() {
        surface.material = .popover;surface.blendingMode = .behindWindow;surface.state = .active
        surface.wantsLayer=true;surface.layer?.cornerRadius=22;surface.layer?.masksToBounds=true
        surface.onFilesDropped={ [weak self] urls in self?.append(urls) }
        header.onMoveEnded={ [weak self] in self?.snapAfterMove() }
        let close=button("xmark",help:"Close Float without deleting files",action:#selector(closeShelf))
        close.setAccessibilityIdentifier("float.close")
        let move=NSImageView(image:NSImage(systemSymbolName:"hand.draw",accessibilityDescription:"Move Float")!)
        move.symbolConfiguration=NSImage.SymbolConfiguration(pointSize:12,weight:.regular);move.setAccessibilityElement(false)
        move.widthAnchor.constraint(equalToConstant:18).isActive=true
        countLabel.font = .systemFont(ofSize:12,weight:.semibold);countLabel.lineBreakMode = .byTruncatingTail
        countLabel.setContentCompressionResistancePriority(.defaultLow,for:.horizontal)
        viewPicker.target=self;viewPicker.action=#selector(changeViewMode);viewPicker.selectedSegment=0;viewPicker.controlSize = .small
        viewPicker.setAccessibilityLabel("Float view");viewPicker.setAccessibilityIdentifier("float.viewMode")
        carryGrip.urls={ [weak self] in self?.actionURLs ?? [] };carryGrip.onDragEnded={ [weak self] in self?.refreshAfterDrag() }
        carryGrip.setAccessibilityIdentifier("float.carry")
        pinButton.image=NSImage(systemSymbolName:"pin",accessibilityDescription:"Attach Float to an edge");pinButton.bezelStyle = .inline;pinButton.isBordered=false;pinButton.target=self;pinButton.action=#selector(showPinMenu);pinButton.toolTip="Attach to any screen edge, or detach"
        pinButton.setAccessibilityIdentifier("float.pin")
        collapseButton.image=NSImage(systemSymbolName:"chevron.up",accessibilityDescription:"Collapse Float");collapseButton.bezelStyle = .inline;collapseButton.isBordered=false;collapseButton.target=self;collapseButton.action=#selector(toggleExpanded)
        collapseButton.setAccessibilityIdentifier("float.collapse")
        let toolbar=NSStackView(views:[close,move,countLabel,carryGrip,viewPicker,pinButton,collapseButton]);toolbar.orientation = .horizontal;toolbar.alignment = .centerY;toolbar.spacing=8;toolbar.edgeInsets=NSEdgeInsets(top:8,left:10,bottom:8,right:10)
        toolbar.translatesAutoresizingMaskIntoConstraints=false;header.addSubview(toolbar)
        NSLayoutConstraint.activate([toolbar.leadingAnchor.constraint(equalTo:header.leadingAnchor),toolbar.trailingAnchor.constraint(equalTo:header.trailingAnchor),toolbar.topAnchor.constraint(equalTo:header.topAnchor),toolbar.bottomAnchor.constraint(equalTo:header.bottomAnchor)])
        header.translatesAutoresizingMaskIntoConstraints=false;surface.addSubview(header)
        headerHeight=header.heightAnchor.constraint(equalToConstant:43)
        flow.minimumInteritemSpacing=8;flow.minimumLineSpacing=8;flow.sectionInset=NSEdgeInsets(top:10,left:10,bottom:10,right:10)
        collection.collectionViewLayout=flow;collection.dataSource=self;collection.delegate=self;collection.isSelectable=true;collection.allowsMultipleSelection=true;collection.allowsEmptySelection=true;collection.backgroundColors=[.clear]
        collection.register(ShelfCollectionItem.self,forItemWithIdentifier:NSUserInterfaceItemIdentifier("FloatFile"))
        collection.registerForDraggedTypes([.fileURL]);collection.setDraggingSourceOperationMask([.copy,.move,.link],forLocal:false);collection.setDraggingSourceOperationMask([.copy,.move],forLocal:true)
        collection.onDoubleClick={ [weak self] in self?.openSelection() }
        collection.onKey={ [weak self] event in self?.handleKey(event) ?? false }
        collection.contextMenu={ [weak self] in self?.makeContextMenu() }
        collection.frame=NSRect(x:0,y:0,width:350,height:230);collection.autoresizingMask=[.width]
        collection.setAccessibilityIdentifier("float.files");collection.setAccessibilityLabel("Files in Float. Drag empty space to select multiple files.")
        scroll.documentView=collection;scroll.hasVerticalScroller=true;scroll.hasHorizontalScroller=false;scroll.autohidesScrollers=true;scroll.drawsBackground=false;scroll.borderType = .noBorder
        scroll.translatesAutoresizingMaskIntoConstraints=false;surface.addSubview(scroll)
        statusLabel.font = .systemFont(ofSize:10);statusLabel.textColor = .secondaryLabelColor;statusLabel.lineBreakMode = .byTruncatingTail
        statusLabel.translatesAutoresizingMaskIntoConstraints=false;surface.addSubview(statusLabel);statusHeight=statusLabel.heightAnchor.constraint(equalToConstant:23)
        NSLayoutConstraint.activate([
            header.leadingAnchor.constraint(equalTo:surface.leadingAnchor),header.trailingAnchor.constraint(equalTo:surface.trailingAnchor),header.topAnchor.constraint(equalTo:surface.topAnchor),headerHeight,
            scroll.leadingAnchor.constraint(equalTo:surface.leadingAnchor),scroll.trailingAnchor.constraint(equalTo:surface.trailingAnchor),scroll.topAnchor.constraint(equalTo:header.bottomAnchor),scroll.bottomAnchor.constraint(equalTo:statusLabel.topAnchor),
            statusLabel.leadingAnchor.constraint(equalTo:surface.leadingAnchor,constant:12),statusLabel.trailingAnchor.constraint(equalTo:surface.trailingAnchor,constant:-12),statusLabel.bottomAnchor.constraint(equalTo:surface.bottomAnchor),statusHeight
        ])
    }

    private func button(_ symbol:String,help:String,action:Selector)->NSButton {
        let result=NSButton(image:NSImage(systemSymbolName:symbol,accessibilityDescription:help)!,target:self,action:action)
        result.bezelStyle = .inline;result.isBordered=false;result.toolTip=help;result.widthAnchor.constraint(equalToConstant:20).isActive=true;return result
    }
    private func reload(preserving ids:Set<UUID>=[]) {
        collection.reloadData();collection.selectionIndexPaths=Set(items.enumerated().filter{ids.contains($0.element.id)}.map{IndexPath(item:$0.offset,section:0)})
        flow.invalidateLayout();updateLabels();updatePreviewIfVisible()
    }
    private func updateLabels() {
        let selected=selectedIndices.count,count=items.count
        countLabel.stringValue="Float · \(count) file\(count==1 ? "":"s")"
        statusLabel.stringValue=selected>0 ? "\(selected) selected · Drag to carry files":"Drop files here · Space for Quick Look"
        collection.emptyMessage=count==0 ? "Drop files or folders here":""
        carryGrip.toolTip=selected>0 ? "Drag all selected files":"Drag all files";carryGrip.needsDisplay=true
        pinButton.image=NSImage(systemSymbolName:attachedEdge==nil ? "pin":"pin.fill",accessibilityDescription:"Attach Float to an edge")
        collapseButton.toolTip=isExpanded ? "Collapse Float":"Expand Float"
        collapseButton.image=NSImage(systemSymbolName:isExpanded ? "chevron.up":"chevron.down",accessibilityDescription:collapseButton.toolTip)
        window?.title="Float · \(count) files"
    }
    private func setFrame(_ frame:NSRect,animate:Bool) {
        isAdjustingFrame=true;defer { isAdjustingFrame=false }
        window?.setFrame(frame,display:true,animate:animate && NashiMotion.enabled)
    }
    private func snapAfterMove() {
        guard let window,let screen=window.screen ?? NSScreen.main else { return }
        let f=window.frame,v=screen.visibleFrame
        let distances:[(FloatShelfEdge,CGFloat)]=[(.left,abs(f.minX-v.minX)),(.right,abs(f.maxX-v.maxX)),(.top,abs(f.maxY-v.maxY)),(.bottom,abs(f.minY-v.minY))]
        if let nearest=distances.min(by:{$0.1<$1.1}),nearest.1<=20 { attach(to:nearest.0) }
        else { attach(to:nil) }
    }
    func windowDidMove(_ notification:Notification) {
        guard !isAdjustingFrame,let edge=attachedEdge,let window,let screen=window.screen else { return }
        let f=window.frame,v=screen.visibleFrame,distance:CGFloat
        switch edge { case .left:distance=abs(f.minX-v.minX);case .right:distance=abs(f.maxX-v.maxX);case .top:distance=abs(f.maxY-v.maxY);case .bottom:distance=abs(f.minY-v.minY) }
        if distance>35 { attachedEdge=nil;updateLabels() }
    }
    func windowDidResize(_ notification:Notification) {
        if isExpanded,!isAdjustingFrame,let window { expandedSize=window.frame.size }
        collection.frame.size.width=scroll.contentSize.width;flow.invalidateLayout()
        if let edge=attachedEdge,!isAdjustingFrame { attach(to:edge) }
    }
    func windowWillClose(_ notification:Notification) {
        guard !isClosed else { return };isClosed=true
        if QLPreviewPanel.sharedPreviewPanelExists(),let panel=QLPreviewPanel.shared(),panel.delegate === self { panel.orderOut(nil);panel.delegate=nil;panel.dataSource=nil }
        for item in items { item.releaseScope() };items=[];previewItems=[]
        onClose?()
    }

    @objc private func closeShelf() { close() }
    @objc private func toggleExpanded() { setExpanded(!isExpanded) }
    func setExpanded(_ value:Bool) {
        guard isExpanded != value,let window else { return }
        let old=window.frame
        if isExpanded { expandedSize=old.size }
        isExpanded=value;scroll.isHidden = !value;statusLabel.isHidden = !value;viewPicker.isHidden = !value
        headerHeight.constant=value ? 43:64;statusHeight.constant=value ? 23:0
        window.minSize=value ? NSSize(width:360,height:200):NSSize(width:250,height:64)
        let size=value ? expandedSize:NSSize(width:265,height:64)
        var origin=NSPoint(x:old.minX,y:old.maxY-size.height)
        if attachedEdge == .right { origin.x=old.maxX-size.width }
        if attachedEdge == .bottom { origin.y=old.minY }
        setFrame(NSRect(origin:origin,size:size),animate:true)
        if let edge=attachedEdge { attach(to:edge) }
        updateLabels();surface.layoutSubtreeIfNeeded()
        if value { window.makeFirstResponder(collection) }
    }
    @objc private func changeViewMode() {
        viewMode=FloatShelfViewMode(rawValue:viewPicker.selectedSegment) ?? .icons
        reload(preserving:Set(selectedIndices.map{items[$0].id}))
    }
    @objc private func showPinMenu(_ sender:NSButton) { let menu=pinMenu();menu.popUp(positioning:nil,at:NSPoint(x:0,y:sender.bounds.minY),in:sender) }
    private func pinMenu()->NSMenu {
        let menu=NSMenu(title:"Attach to Edge")
        for edge in FloatShelfEdge.allCases {
            let item=NSMenuItem(title:edge.rawValue.capitalized,action:#selector(pinChoice),keyEquivalent:"");item.target=self;item.representedObject=edge.rawValue;item.state=attachedEdge==edge ? .on:.off;menu.addItem(item)
        }
        menu.addItem(.separator());let detach=NSMenuItem(title:"Detach",action:#selector(pinChoice),keyEquivalent:"");detach.target=self;detach.state=attachedEdge==nil ? .on:.off;menu.addItem(detach);return menu
    }
    @objc private func pinChoice(_ sender:NSMenuItem) { attach(to:(sender.representedObject as? String).flatMap(FloatShelfEdge.init(rawValue:))) }

    func numberOfSections(in collectionView:NSCollectionView)->Int { 1 }
    func collectionView(_ collectionView:NSCollectionView,numberOfItemsInSection section:Int)->Int { items.count }
    func collectionView(_ collectionView:NSCollectionView,itemForRepresentedObjectAt indexPath:IndexPath)->NSCollectionViewItem {
        let item=collectionView.makeItem(withIdentifier:NSUserInterfaceItemIdentifier("FloatFile"),for:indexPath) as! ShelfCollectionItem
        item.configure(url:items[indexPath.item].url,mode:viewMode);return item
    }
    func collectionView(_ collectionView:NSCollectionView,layout collectionViewLayout:NSCollectionViewLayout,sizeForItemAt indexPath:IndexPath)->NSSize {
        viewMode == .list ? NSSize(width:max(100,scroll.contentSize.width-20),height:48):NSSize(width:94,height:101)
    }
    func collectionView(_ collectionView:NSCollectionView,didSelectItemsAt indexPaths:Set<IndexPath>) { updateLabels();updatePreviewIfVisible() }
    func collectionView(_ collectionView:NSCollectionView,didDeselectItemsAt indexPaths:Set<IndexPath>) { updateLabels();updatePreviewIfVisible() }
    func collectionView(_ collectionView:NSCollectionView,canDragItemsAt indexPaths:Set<IndexPath>,with event:NSEvent)->Bool { !indexPaths.isEmpty }
    func collectionView(_ collectionView:NSCollectionView,pasteboardWriterForItemAt indexPath:IndexPath)->NSPasteboardWriting? { items[indexPath.item].url as NSURL }
    func collectionView(_ collectionView:NSCollectionView,draggingSession session:NSDraggingSession,endedAt screenPoint:NSPoint,dragOperation operation:NSDragOperation) { refreshAfterDrag() }
    func collectionView(_ collectionView:NSCollectionView,validateDrop draggingInfo:NSDraggingInfo,proposedIndexPath proposedDropIndexPath:AutoreleasingUnsafeMutablePointer<NSIndexPath>,dropOperation proposedDropOperation:UnsafeMutablePointer<NSCollectionView.DropOperation>)->NSDragOperation {
        if let source=draggingInfo.draggingSource as? NSCollectionView,source === collectionView { return [] }
        guard !ShelfDropSurface.fileURLs(draggingInfo.draggingPasteboard).isEmpty else { return [] }
        proposedDropIndexPath.pointee=IndexPath(item:items.count,section:0) as NSIndexPath;proposedDropOperation.pointee = .before;return .copy
    }
    func collectionView(_ collectionView:NSCollectionView,acceptDrop draggingInfo:NSDraggingInfo,indexPath:IndexPath,dropOperation:NSCollectionView.DropOperation)->Bool {
        let values=ShelfDropSurface.fileURLs(draggingInfo.draggingPasteboard);guard !values.isEmpty else { return false };append(values);return true
    }
    private func refreshAfterDrag() { collection.reloadData();updateLabels() }

    private func handleKey(_ event:NSEvent)->Bool {
        let command=event.modifierFlags.contains(.command)
        if command {
            switch event.charactersIgnoringModifiers?.lowercased() {
            case "a":collection.selectAll(nil);return true
            case "c":if event.modifierFlags.contains(.option) { copyPaths() } else { copyFiles() };return true
            case "v":append(ShelfDropSurface.fileURLs(.general));return true
            case "o":openSelection();return true
            case "r":revealSelection();return true
            default:return false
            }
        }
        if event.keyCode==49 { showQuickLook();return true }
        if event.keyCode==36 || event.keyCode==76 { openSelection();return true }
        if event.keyCode==51 || event.keyCode==117 { removeSelection();return true }
        if event.keyCode==53 { if !collection.selectionIndexPaths.isEmpty { collection.selectionIndexPaths=[];updateLabels() } else { setExpanded(false) };return true }
        guard !event.modifierFlags.contains(.control),!event.modifierFlags.contains(.option),let value=event.characters,!value.isEmpty,value.unicodeScalars.allSatisfy({CharacterSet.alphanumerics.contains($0) || $0==" "}) else { return false }
        let time=ProcessInfo.processInfo.systemUptime,query=value.lowercased()
        if time-lastTypeTime>1 { typeBuffer="" };lastTypeTime=time
        let repeated=typeBuffer==query
        typeBuffer=repeated ? query:typeBuffer+query
        let start=repeated ? (selectedIndices.first ?? -1)+1:0
        for offset in items.indices {
            let index=(start+offset)%items.count
            if items[index].url.lastPathComponent.lowercased().hasPrefix(typeBuffer) {
                collection.selectItems(at:[IndexPath(item:index,section:0)],scrollPosition:.centeredVertically);return true
            }
        }
        return !items.isEmpty
    }

    private func makeContextMenu()->NSMenu {
        let menu=NSMenu(title:"Float")
        let commands:[(String,Selector)]=[("Open",#selector(openSelection)),("Quick Look",#selector(showQuickLook)),("Show in Finder",#selector(revealSelection)),("Copy",#selector(copyFiles)),("Copy Path",#selector(copyPaths)),("Share…",#selector(shareSelection)),("Remove from Float",#selector(removeSelection))]
        for (title,action) in commands { let item=NSMenuItem(title:title,action:action,keyEquivalent:"");item.target=self;menu.addItem(item) }
        menu.addItem(.separator())
        let add=NSMenuItem(title:"Add Files…",action:#selector(chooseFiles),keyEquivalent:"");add.target=self;menu.addItem(add)
        let clear=NSMenuItem(title:"Clear Float",action:#selector(clearShelf),keyEquivalent:"");clear.target=self;menu.addItem(clear)
        let pin=NSMenuItem(title:"Attach to Edge",action:nil,keyEquivalent:"");pin.submenu=pinMenu();menu.addItem(pin)
        return menu
    }
    func validateMenuItem(_ menuItem:NSMenuItem)->Bool {
        if menuItem.action == #selector(chooseFiles) { return true }
        if menuItem.action == #selector(removeSelection) { return !selectedIndices.isEmpty }
        if menuItem.action == #selector(pinChoice) { return true }
        return !items.isEmpty
    }
    @objc private func openSelection() {
        for url in actionURLs { if !NSWorkspace.shared.open(url) { statusLabel.stringValue="Could not open \(url.lastPathComponent)." } }
    }
    @objc private func revealSelection() { if !actionURLs.isEmpty { NSWorkspace.shared.activateFileViewerSelecting(actionURLs) } }
    @objc private func copyFiles() { NSPasteboard.general.clearContents();NSPasteboard.general.writeObjects(actionURLs.map{$0 as NSURL}) }
    @objc private func copyPaths() { NSPasteboard.general.clearContents();NSPasteboard.general.setString(actionURLs.map(\.path).joined(separator:"\n"),forType:.string) }
    @objc private func shareSelection() {
        guard !actionURLs.isEmpty else { return }
        sharingPicker=NSSharingServicePicker(items:actionURLs);sharingPicker?.show(relativeTo:carryGrip.bounds,of:carryGrip,preferredEdge:.minY)
    }
    @objc private func chooseFiles() {
        let panel=NSOpenPanel();panel.canChooseDirectories=true;panel.canChooseFiles=true;panel.allowsMultipleSelection=true;panel.prompt="Add to Float"
        if panel.runModal() == .OK { append(panel.urls) }
    }
    @objc private func removeSelection() {
        let selected=Set(selectedIndices)
        for index in selected { items[index].releaseScope() }
        items=items.enumerated().filter{!selected.contains($0.offset)}.map(\.element);reload()
    }
    @objc private func clearShelf() { for item in items { item.releaseScope() };items=[];reload() }
    @objc private func showQuickLook() {
        guard !actionURLs.isEmpty,let panel=QLPreviewPanel.shared() else { return }
        previewItems=actionURLs.map(ShelfPreviewItem.init);window?.makeFirstResponder(collection)
        panel.updateController();panel.dataSource=self;panel.delegate=self;panel.reloadData();panel.makeKeyAndOrderFront(nil)
    }
    private func updatePreviewIfVisible() {
        guard QLPreviewPanel.sharedPreviewPanelExists(),let panel=QLPreviewPanel.shared(),panel.isVisible,panel.delegate === self else { return }
        previewItems=actionURLs.map(ShelfPreviewItem.init);panel.reloadData()
        if previewItems.isEmpty { panel.orderOut(nil) }
    }
    override func acceptsPreviewPanelControl(_ panel:QLPreviewPanel!)->Bool { !items.isEmpty }
    override func beginPreviewPanelControl(_ panel:QLPreviewPanel!) { panel.dataSource=self;panel.delegate=self }
    override func endPreviewPanelControl(_ panel:QLPreviewPanel!) { panel.dataSource=nil;panel.delegate=nil }
    func numberOfPreviewItems(in panel:QLPreviewPanel!)->Int { previewItems.count }
    func previewPanel(_ panel:QLPreviewPanel!,previewItemAt index:Int)->QLPreviewItem! { previewItems.indices.contains(index) ? previewItems[index]:nil }
    func previewPanel(_ panel:QLPreviewPanel!,handle event:NSEvent!)->Bool {
        if event.type == .keyDown,event.keyCode==49 { panel.orderOut(nil);return true };return false
    }
}

@MainActor private final class ShelfFile {
    let id=UUID()
    let url:URL
    private var scoped:Bool
    init(url:URL) { self.url=url;scoped=url.startAccessingSecurityScopedResource() }
    deinit { if scoped { url.stopAccessingSecurityScopedResource() } }
    func releaseScope() { if scoped { url.stopAccessingSecurityScopedResource();scoped=false } }
}
private final class ShelfPreviewItem:NSObject,QLPreviewItem {
    let url:URL
    init(_ url:URL) { self.url=url }
    var previewItemURL:URL? { url }
    var previewItemTitle:String? { url.lastPathComponent }
}
private final class ShelfPanel:NSPanel {
    override var canBecomeKey:Bool { true }
    override var canBecomeMain:Bool { false }
}
@MainActor private final class ShelfMoveHeader:NSView {
    var onMoveEnded:(()->Void)?
    override func mouseDown(with event:NSEvent) { window?.performDrag(with:event);onMoveEnded?() }
}
@MainActor private final class ShelfDropSurface:NSVisualEffectView {
    var onFilesDropped:(([URL])->Void)?
    override init(frame frameRect:NSRect) { super.init(frame:frameRect);registerForDraggedTypes([.fileURL]) }
    required init?(coder:NSCoder) { super.init(coder:coder);registerForDraggedTypes([.fileURL]) }
    static func fileURLs(_ pasteboard:NSPasteboard)->[URL] { (pasteboard.readObjects(forClasses:[NSURL.self],options:[.urlReadingFileURLsOnly:true]) as? [URL]) ?? [] }
    override func draggingEntered(_ sender:NSDraggingInfo)->NSDragOperation { Self.fileURLs(sender.draggingPasteboard).isEmpty ? []:.copy }
    override func draggingUpdated(_ sender:NSDraggingInfo)->NSDragOperation { draggingEntered(sender) }
    override func prepareForDragOperation(_ sender:NSDraggingInfo)->Bool { !Self.fileURLs(sender.draggingPasteboard).isEmpty }
    override func performDragOperation(_ sender:NSDraggingInfo)->Bool { let urls=Self.fileURLs(sender.draggingPasteboard);guard !urls.isEmpty else { return false };onFilesDropped?(urls);return true }
}
@MainActor private final class ShelfCollectionView:NSCollectionView {
    var onDoubleClick:(()->Void)?
    var onKey:((NSEvent)->Bool)?
    var contextMenu:(()->NSMenu?)?
    var emptyMessage="" { didSet { needsDisplay=true } }
    override func keyDown(with event:NSEvent) { if onKey?(event) != true { super.keyDown(with:event) } }
    override func mouseDown(with event:NSEvent) {
        let clickedItem=indexPathForItem(at:convert(event.locationInWindow,from:nil)) != nil
        super.mouseDown(with:event);if event.clickCount==2,clickedItem { onDoubleClick?() }
    }
    override func menu(for event:NSEvent)->NSMenu? {
        let point=convert(event.locationInWindow,from:nil)
        if let path=indexPathForItem(at:point),!selectionIndexPaths.contains(path) { selectionIndexPaths=[path] }
        return contextMenu?()
    }
    override func layout() {
        super.layout()
        if let clip=superview as? NSClipView,frame.height<clip.bounds.height { frame.size.height=clip.bounds.height }
    }
    override func draw(_ dirtyRect:NSRect) {
        super.draw(dirtyRect)
        if !emptyMessage.isEmpty {
            let paragraph=NSMutableParagraphStyle();paragraph.alignment = .center
            (emptyMessage as NSString).draw(in:NSRect(x:15,y:bounds.midY-10,width:max(0,bounds.width-30),height:35),withAttributes:[.font:NSFont.systemFont(ofSize:12),.foregroundColor:NSColor.secondaryLabelColor,.paragraphStyle:paragraph])
        }
    }
}
@MainActor private final class ShelfCollectionItem:NSCollectionViewItem {
    private let icon=NSImageView()
    private let name=NSTextField(wrappingLabelWithString:"")
    private let detail=NSTextField(labelWithString:"")
    private var url:URL?
    private var mode:FloatShelfViewMode = .icons
    private var thumbnailRequest:QLThumbnailGenerator.Request?
    override var isSelected:Bool { didSet { updateSelection() } }
    override func loadView() {
        view=NSView();view.wantsLayer=true;view.layer?.cornerRadius=13;view.layer?.borderWidth=1
        icon.imageScaling = .scaleProportionallyUpOrDown
        name.font = .systemFont(ofSize:11);name.maximumNumberOfLines=2;name.isSelectable=false;name.lineBreakMode = .byTruncatingMiddle
        detail.font = .systemFont(ofSize:9);detail.textColor = .secondaryLabelColor;detail.lineBreakMode = .byTruncatingMiddle
        view.addSubview(icon);view.addSubview(name);view.addSubview(detail)
        view.setAccessibilityElement(true);view.setAccessibilityRole(.button)
    }
    override func viewDidLayout() {
        super.viewDidLayout()
        if mode == .icons {
            icon.frame=NSRect(x:(view.bounds.width-52)/2,y:42,width:52,height:52)
            name.alignment = .center;name.frame=NSRect(x:4,y:3,width:max(0,view.bounds.width-8),height:35);detail.isHidden=true
        } else {
            icon.frame=NSRect(x:7,y:8,width:32,height:32);name.alignment = .left;name.frame=NSRect(x:49,y:24,width:max(0,view.bounds.width-57),height:17)
            detail.isHidden=false;detail.frame=NSRect(x:49,y:7,width:max(0,view.bounds.width-57),height:14)
        }
    }
    func configure(url:URL,mode:FloatShelfViewMode) {
        _=view;self.url=url;self.mode=mode
        if let request=thumbnailRequest { QLThumbnailGenerator.shared.cancel(request) }
        name.stringValue=url.lastPathComponent;detail.stringValue=url.deletingLastPathComponent().path;icon.image=NSWorkspace.shared.icon(forFile:url.path)
        view.toolTip=url.path;view.setAccessibilityLabel(url.lastPathComponent);view.needsLayout=true;updateSelection()
        let request=QLThumbnailGenerator.Request(fileAt:url,size:NSSize(width:96,height:96),scale:NSScreen.main?.backingScaleFactor ?? 2,representationTypes:.all)
        thumbnailRequest=request
        QLThumbnailGenerator.shared.generateBestRepresentation(for:request) { [weak self] representation,_ in
            DispatchQueue.main.async { guard let self,self.url==url,let representation else { return };self.icon.image=representation.nsImage }
        }
    }
    private func updateSelection() {
        guard isViewLoaded else { return }
        view.layer?.backgroundColor=(isSelected ? NashiPalette.gold.withAlphaComponent(0.18):NSColor.clear).cgColor
        view.layer?.borderColor=(isSelected ? NashiPalette.gold.withAlphaComponent(0.7):NSColor.clear).cgColor
        view.setAccessibilitySelected(isSelected)
    }
}
@MainActor private final class ShelfCarryGrip:NSView,NSDraggingSource {
    var urls:(()->[URL])?
    var onDragEnded:(()->Void)?
    private var origin=NSPoint.zero
    private var sessionActive=false
    override init(frame frameRect:NSRect) { super.init(frame:frameRect);widthAnchor.constraint(equalToConstant:27).isActive=true;heightAnchor.constraint(equalToConstant:24).isActive=true;setAccessibilityElement(true);setAccessibilityRole(.button);setAccessibilityLabel("Drag all selected files, or all Float files") }
    required init?(coder:NSCoder) { super.init(coder:coder) }
    override func draw(_ dirtyRect:NSRect) {
        NashiPalette.gold.withAlphaComponent(0.1).setFill();NSBezierPath(roundedRect:bounds,xRadius:5,yRadius:5).fill()
        NSImage(systemSymbolName:"square.stack.3d.up",accessibilityDescription:nil)?.draw(in:bounds.insetBy(dx:5,dy:4))
    }
    override func mouseDown(with event:NSEvent) { origin=convert(event.locationInWindow,from:nil) }
    override func mouseDragged(with event:NSEvent) {
        let point=convert(event.locationInWindow,from:nil),values=urls?() ?? []
        guard !sessionActive,!values.isEmpty,hypot(point.x-origin.x,point.y-origin.y)>3 else { return }
        let items=values.enumerated().map { index,url in
            let item=NSDraggingItem(pasteboardWriter:url as NSURL),image=NSWorkspace.shared.icon(forFile:url.path).copy() as! NSImage;image.size=NSSize(width:42,height:42)
            item.setDraggingFrame(NSRect(x:point.x+CGFloat(index%5)*3,y:point.y+CGFloat(index%5)*3,width:42,height:42),contents:image);return item
        }
        sessionActive=true;beginDraggingSession(with:items,event:event,source:self)
    }
    func draggingSession(_ session:NSDraggingSession,sourceOperationMaskFor context:NSDraggingContext)->NSDragOperation { [.copy,.move,.link] }
    func draggingSession(_ session:NSDraggingSession,endedAt screenPoint:NSPoint,operation:NSDragOperation) { sessionActive=false;onDragEnded?() }
}
