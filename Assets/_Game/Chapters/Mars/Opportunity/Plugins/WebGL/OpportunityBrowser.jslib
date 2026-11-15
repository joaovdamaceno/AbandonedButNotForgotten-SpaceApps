mergeInto(LibraryManager.library, {
  OpportunityBrowser_Register: function(name) {
    var bridge=window.opportunityBrowser;
    if(bridge){bridge.audioUnlock=function(){if(typeof WEBAudio!=="undefined"&&WEBAudio.audioContext&&WEBAudio.audioContext.state!=="running")WEBAudio.audioContext.resume().catch(function(){});};bridge.audioState=function(){return typeof WEBAudio!=="undefined"&&WEBAudio.audioContext?WEBAudio.audioContext.state:"unavailable";};bridge.register(UTF8ToString(name));}
  },
  OpportunityBrowser_ResumeAudio: function() {
    if(typeof WEBAudio!=="undefined"&&WEBAudio.audioContext&&WEBAudio.audioContext.state!=="running")WEBAudio.audioContext.resume().catch(function(){});
    if(window.opportunityBrowser)window.opportunityBrowser.unlockAudio();
  },
  OpportunityBrowser_Fullscreen: function() {
    if(window.opportunityBrowser)window.opportunityBrowser.fullscreen();
  },
  OpportunityBrowser_ResumeOverlay: function(visible) {
    if(window.opportunityBrowser)window.opportunityBrowser.resumeOverlay(visible!==0);
  },
  OpportunityBrowser_Snapshot: function(data) {
    if(window.opportunityBrowser)window.opportunityBrowser.snapshot(UTF8ToString(data));
  },
  OpportunityBrowser_IsQA: function() {
    return new URLSearchParams(window.location.search).get("qa")==="1"?1:0;
  }
});
