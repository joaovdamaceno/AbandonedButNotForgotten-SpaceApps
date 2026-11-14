mergeInto(LibraryManager.library, {
  SUR007_Report: function(jsonPtr) {
    var report = document.getElementById('sur007qa');
    if (!report) {
      report = document.createElement('pre');
      report.id = 'sur007qa';
      report.style.cssText = 'position:fixed;right:8px;bottom:8px;width:320px;max-height:160px;overflow:auto;white-space:pre-wrap;color:#eee9dd;background:#080c13dd;padding:10px;margin:0;font:10px monospace;z-index:10;pointer-events:none';
      document.body.appendChild(report);
    }
    report.textContent = UTF8ToString(jsonPtr);
    report.setAttribute('role', 'status');
  }
});
