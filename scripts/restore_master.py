import base64, hashlib, io, json, pathlib, re, shutil, sys, uuid, zipfile
text = pathlib.Path(sys.argv[1]).read_text(encoding="utf-8-sig")
match = re.search(r"\n===== V\d+\.\d+ SOURCE ZIP BASE64 BEGIN =====\n", text)
if not match:
    raise SystemExit("Source payload marker not found")
meta, payload = text[:match.start()], text[match.end():]
payload = re.split(r"\n===== V\d+\.\d+ SOURCE ZIP BASE64 END =====", payload, maxsplit=1)[0]
data = base64.b64decode(payload)
expected = meta.rsplit("SOURCE_ZIP_SHA256=", 1)[1].splitlines()[0]
if hashlib.sha256(data).hexdigest() != expected:
    raise SystemExit("Source payload checksum mismatch")
dest = pathlib.Path(sys.argv[2])
if dest.exists():
    raise SystemExit("Choose a new destination directory")
temp_dest = dest.parent / f".{dest.name}.restore-{uuid.uuid4().hex}"
with zipfile.ZipFile(io.BytesIO(data)) as archive:
    normalized = set()
    for info in archive.infolist():
        p = pathlib.PurePosixPath(info.filename)
        if p.is_absolute() or ".." in p.parts or "\\" in info.filename or ":" in info.filename:
            raise SystemExit("Unsafe archive path")
        key = "/".join(part.rstrip(" .").casefold() for part in p.parts)
        if key in normalized:
            raise SystemExit("Duplicate archive path")
        normalized.add(key)
    damaged = archive.testzip()
    if damaged:
        raise SystemExit(f"Damaged archive member: {damaged}")
    try:
        archive.extractall(temp_dest)
        temp_dest.replace(dest)
    finally:
        if temp_dest.exists():
            shutil.rmtree(temp_dest, ignore_errors=True)
print("Restored source to", dest)
