param([int]$Port = 8765, [string]$Root = (Join-Path $PSScriptRoot '..\..\..\Builds\SUR-009-Web'))
# Servidor local (127.0.0.1) para o build WebGL gzip do Unity. Apenas para QA.
$Root = (Resolve-Path $Root).Path
$listener = New-Object System.Net.HttpListener
$listener.Prefixes.Add("http://127.0.0.1:$Port/")
$listener.Start()
Write-Host "serving $Root on http://127.0.0.1:$Port/"
$types = @{ '.html'='text/html'; '.js'='application/javascript'; '.wasm'='application/wasm'; '.data'='application/octet-stream'; '.css'='text/css'; '.png'='image/png'; '.jpg'='image/jpeg'; '.ico'='image/x-icon'; '.json'='application/json'; '.svg'='image/svg+xml' }
while ($listener.IsListening) {
  $ctx = $listener.GetContext()
  try {
    $path = [Uri]::UnescapeDataString($ctx.Request.Url.AbsolutePath).TrimStart('/')
    if ($ctx.Request.HttpMethod -eq 'PUT' -and $path -eq 'save') {
      # Recebe as capturas do proprio jogo (so nomes simples, gravadas em QA/SUR-009/Captures/Web).
      $name = [IO.Path]::GetFileName($ctx.Request.QueryString['name'])
      $dir = Join-Path $PSScriptRoot '..\Captures\Web'
      New-Item -ItemType Directory -Force $dir | Out-Null
      $ms = New-Object IO.MemoryStream
      $ctx.Request.InputStream.CopyTo($ms)
      [IO.File]::WriteAllBytes((Join-Path (Resolve-Path $dir).Path $name), $ms.ToArray())
      $ctx.Response.StatusCode = 200; $ctx.Response.Close(); Write-Host "saved $name"; continue
    }
    if ($path -eq '') { $path = 'index.html' }
    $file = Join-Path $Root $path
    if (-not (Test-Path $file -PathType Leaf)) { $ctx.Response.StatusCode = 404; $ctx.Response.Close(); continue }
    $inner = $file
    if ($file.EndsWith('.gz')) { $ctx.Response.AddHeader('Content-Encoding', 'gzip'); $inner = $file.Substring(0, $file.Length - 3) }
    $ext = [IO.Path]::GetExtension($inner).ToLower()
    $ctx.Response.ContentType = if ($types.ContainsKey($ext)) { $types[$ext] } else { 'application/octet-stream' }
    $bytes = [IO.File]::ReadAllBytes($file)
    $ctx.Response.ContentLength64 = $bytes.Length
    $ctx.Response.OutputStream.Write($bytes, 0, $bytes.Length)
  } catch { Write-Host $_.Exception.Message }
  $ctx.Response.Close()
}
