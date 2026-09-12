# Source snapshots

This directory keeps release-specific source references for Third-Person Mode.

- `ThirdPersonMode.v1.0.1.cs` is the historical v1.0.1 gameplay source.
- `manifest.v1.0.1.json` is the historical v1.0.1 manifest.
- `manifest.v1.1.5.json` is the v1.1.5 release manifest.
- `ThirdPersonMode.v1.1.5.cs.gz.b64.part1` through `part3` together contain the exact v1.1.5 gameplay source, gzip-compressed and Base64-encoded.

The v1.1.5 source is stored in three text parts so the release snapshot remains byte-exact. It reconstructs to `ThirdPersonMode.cs` with SHA-256:

`9AF4B193DF668A5387CE1A4EB5E83F4DDCE1E2E620AD26AF724FC9C5DCF852A4`

## Reconstruct on Linux/macOS/Git Bash

```bash
cat ThirdPersonMode.v1.1.5.cs.gz.b64.part1 \
    ThirdPersonMode.v1.1.5.cs.gz.b64.part2 \
    ThirdPersonMode.v1.1.5.cs.gz.b64.part3 \
  | base64 -d \
  | gzip -dc \
  > ThirdPersonMode.cs
```

## Reconstruct with Windows PowerShell

```powershell
$parts = 1..3 | ForEach-Object {
    Get-Content ".\\ThirdPersonMode.v1.1.5.cs.gz.b64.part$_" -Raw
}
$compressed = [Convert]::FromBase64String(($parts -join ''))
$input = [IO.MemoryStream]::new($compressed)
$gzip = [IO.Compression.GZipStream]::new(
    $input,
    [IO.Compression.CompressionMode]::Decompress
)
$output = [IO.File]::Create((Join-Path $PWD 'ThirdPersonMode.cs'))
$gzip.CopyTo($output)
$output.Dispose()
$gzip.Dispose()
$input.Dispose()
```

The public repository intentionally does not include the release-builder package. Builders are development artifacts; published source and release metadata remain here for review and preservation.
