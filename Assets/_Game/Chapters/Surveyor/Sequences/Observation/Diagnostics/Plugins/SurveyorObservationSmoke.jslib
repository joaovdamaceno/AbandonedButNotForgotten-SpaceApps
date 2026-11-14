mergeInto(LibraryManager.library, {
 SUR008_Report: function(ptr) {
  var node=document.getElementById('sur008qa');
  if(!node){node=document.createElement('pre');node.id='sur008qa';node.style.cssText='position:fixed;right:8px;bottom:8px;width:320px;max-height:140px;overflow:auto;background:#080c13dd;color:#eee9dd;padding:8px;font:10px monospace;pointer-events:none;z-index:10';document.body.appendChild(node);}
  node.textContent=UTF8ToString(ptr);node.setAttribute('role','status');
 }
});
