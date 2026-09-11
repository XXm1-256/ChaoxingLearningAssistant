import base64, hashlib, io, json, pathlib, sys, zipfile
text = pathlib.Path(sys.argv[1]).read_text(encoding="utf-8-sig")
meta, payload = text.split("\n===== V1.32 SOURCE ZIP BASE64 BEGIN =====\n", 1)
payload = payload.split("\n===== V1.32 SOURCE ZIP BASE64 END =====", 1)[0]
data = base64.b64decode(payload)
expected = meta.rsplit("SOURCE_ZIP_SHA256=", 1)[1].splitlines()[0]
if hashlib.sha256(data).hexdigest() != expected:
    raise SystemExit("Source payload checksum mismatch")
dest = pathlib.Path(sys.argv[2])
if dest.exists():
    raise SystemExit("Choose a new destination directory")
with zipfile.ZipFile(io.BytesIO(data)) as archive:
    for info in archive.infolist():
        p = pathlib.PurePosixPath(info.filename)
        if p.is_absolute() or ".." in p.parts or "\\" in info.filename or ":" in info.filename:
            raise SystemExit("Unsafe archive path")
    archive.extractall(dest)
print("Restored source to", dest)
