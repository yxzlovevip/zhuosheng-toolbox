"""Run as root after uploading cz-updates.conf to /var/www/cz-updates/ops/."""
import datetime
import hashlib
import pathlib
import shutil
import subprocess

site = pathlib.Path('/etc/nginx/sites-available/zskj.top')
snippet = pathlib.Path('/etc/nginx/snippets/cz-updates.conf')
source = pathlib.Path('/var/www/cz-updates/ops/cz-updates.conf')
include = '    include /etc/nginx/snippets/cz-updates.conf;\n'
anchor = '    include /etc/nginx/snippets/beifen-updates.conf;\n'
original = site.read_bytes()
text = original.decode()
if include not in text and text.count(anchor) != 1:
    raise SystemExit('Cannot uniquely locate the existing HTTPS server; nothing changed.')

protected = [pathlib.Path('/etc/nginx/snippets/cpan-updates.conf'),
             pathlib.Path('/etc/nginx/snippets/beifen-updates.conf'),
             pathlib.Path('/var/www/cpan-updates/latest.json')]
before = {str(p): hashlib.sha256(p.read_bytes()).hexdigest() for p in protected if p.exists()}
current = pathlib.Path('/var/www/zskj/current').resolve()
stamp = datetime.datetime.now(datetime.timezone.utc).strftime('%Y%m%dT%H%M%S%fZ')
backup = pathlib.Path('/var/backups/cz-updates') / stamp
backup.mkdir(parents=True)
shutil.copy2(site, backup / 'zskj.top')
previous_snippet = snippet.read_bytes() if snippet.exists() else None
if previous_snippet is not None:
    shutil.copy2(snippet, backup / snippet.name)

try:
    shutil.copyfile(source, snippet)
    snippet.chmod(0o644)
    if include not in text:
        # Write in place to retain ownership/mode and the sites-enabled link.
        site.write_text(text.replace(anchor, anchor + include, 1))
    subprocess.run(['nginx', '-t'], check=True)
    subprocess.run(['systemctl', 'reload', 'nginx'], check=True)
    subprocess.run(['systemctl', 'is-active', '--quiet', 'nginx'], check=True)
    assert pathlib.Path('/var/www/zskj/current').resolve() == current
    for p, digest in before.items():
        assert hashlib.sha256(pathlib.Path(p).read_bytes()).hexdigest() == digest, p
except BaseException:
    site.write_bytes(original)
    if previous_snippet is None:
        snippet.unlink(missing_ok=True)
    else:
        snippet.write_bytes(previous_snippet)
    subprocess.run(['nginx', '-t'], check=True)
    subprocess.run(['systemctl', 'reload', 'nginx'], check=True)
    raise
print('Configured: https://zskj.top/cz-updates/latest.json')
print('Backup:', backup)
print('Existing website, Cpan and backup-assistant configuration unchanged.')
