#!/usr/bin/env python3
"""Change only this game's Steam LaunchOptions; preserve all unrelated VDF bytes."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import tempfile
import time

APP_ID = '3548580'
PROJECT = Path(__file__).resolve().parents[2]
STEAM = Path.home() / 'Library/Application Support/Steam'

class Entry:
    def __init__(self, key, value, start, value_start, end, children=None, close=None):
        self.key, self.value, self.start, self.value_start, self.end = key, value, start, value_start, end
        self.children, self.close = children, close

def parse(text):
    tokens = []
    pattern = re.compile(r'\s+|//[^\n]*|"(?:\\.|[^"\\])*"|[{}]', re.DOTALL)
    pos = 0
    while pos < len(text):
        m = pattern.match(text, pos)
        if not m: raise ValueError('Unsupported VDF syntax at offset %d' % pos)
        raw = m.group(); start = pos; pos = m.end()
        if raw.isspace() or raw.startswith('//'): continue
        value = raw
        if raw.startswith('"'):
            value = re.sub(r'\\([\\"nrt])', lambda m: {'n':'\n','r':'\r','t':'\t','"':'"','\\':'\\'}[m.group(1)], raw[1:-1])
        tokens.append((value, start, pos, raw))
    index = 0
    def block(nested=False):
        nonlocal index
        entries = []
        while index < len(tokens):
            key, start, _, raw = tokens[index]
            if raw == '}':
                if not nested: raise ValueError('Unexpected closing brace')
                index += 1
                return entries, start
            if not raw.startswith('"'): raise ValueError('Expected quoted key')
            index += 1
            if index >= len(tokens): raise ValueError('Missing value')
            value, value_start, end, raw_value = tokens[index]; index += 1
            if raw_value == '{':
                children, close = block(True)
                entries.append(Entry(key, None, start, value_start, close + 1, children, close))
            elif raw_value.startswith('"'):
                entries.append(Entry(key, value, start, value_start, end))
            else: raise ValueError('Expected string or block')
        if nested: raise ValueError('Unclosed block')
        return entries, None
    return block()[0]

def child(entries, name, required=True):
    matches = [e for e in entries if e.key.lower() == name.lower()]
    if len(matches) > 1: raise ValueError('Duplicate VDF key: ' + name)
    if not matches:
        if required: raise ValueError('Missing VDF key: ' + name)
        return None
    return matches[0]

def app_entry(text):
    entries = parse(text)
    for name in ['UserLocalConfigStore', 'Software', 'Valve', 'Steam', 'apps', APP_ID]:
        entry = child(entries, name)
        entries = entry.children
        if entries is None: raise ValueError('Expected block at ' + name)
    return entry

def read_option(text):
    entry = child(app_entry(text).children, 'LaunchOptions', False)
    return None if entry is None else entry.value

def set_option(text, value):
    app = app_entry(text)
    entry = child(app.children, 'LaunchOptions', False)
    if entry:
        if value is None: return text[:entry.start] + text[entry.end:]
        return text[:entry.value_start] + json.dumps(value, ensure_ascii=False) + text[entry.end:]
    if value is None: return text
    line_start = text.rfind('\n', 0, app.close) + 1
    indent = text[line_start:app.close]
    if indent.strip(): raise ValueError('Cannot safely infer VDF indentation')
    newline = '\r\n' if '\r\n' in text else '\n'
    return text[:line_start] + indent + '\t"LaunchOptions"\t\t' + json.dumps(value, ensure_ascii=False) + newline + text[line_start:]

def select_user(users):
    active = [e for e in users if child(e.children, 'MostRecent', False) and child(e.children, 'MostRecent').value == '1']
    if len(active) == 1:
        return active[0]
    # Recent Steam clients can omit MostRecent. A single saved account is
    # unambiguous; never guess between multiple accounts or use credentials.
    if not active and len(users) == 1:
        return users[0]
    raise RuntimeError('无法唯一确定当前 Steam 用户。')

def active_config():
    with (STEAM/'config/loginusers.vdf').open(newline='') as f: users = child(parse(f.read()), 'users').children
    account = int(select_user(users).key) - 76561197960265728
    path = STEAM/'userdata'/str(account)/'config/localconfig.vdf'
    if not path.is_file(): raise RuntimeError('找不到当前账号的 Steam 配置。')
    return path

def atomic_write(path, text):
    fd, tmp = tempfile.mkstemp(prefix=path.name + '.musicbridge-', dir=path.parent)
    try:
        os.fchmod(fd, path.stat().st_mode & 0o777)
        with os.fdopen(fd, 'w', newline='') as f: f.write(text); f.flush(); os.fsync(f.fileno())
        os.replace(tmp, path)
    finally:
        if os.path.exists(tmp): os.unlink(tmp)

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('action', choices=['inspect','install','restore'])
    args = parser.parse_args()
    path = active_config()
    with path.open(newline='') as f: text = f.read()
    old = read_option(text)
    core = PROJECT/'macos/dist/ChillMusicMac/launch-core.sh'
    wanted = json.dumps(str(core), ensure_ascii=False) + ' --steam %command%'
    if args.action == 'inspect':
        print('配置文件：', path)
        print('当前启动选项：', old or '(空)')
        print('准备设置为：', wanted)
        return
    if subprocess.run(['/usr/bin/pgrep','-x','steam_osx'], stdout=subprocess.DEVNULL).returncode == 0:
        raise RuntimeError('请先正常退出 Steam，避免它覆盖磁盘上的启动配置。')
    if subprocess.run(['/usr/bin/pgrep','-x','Chill With You'], stdout=subprocess.DEVNULL).returncode == 0:
        raise RuntimeError('请先正常退出游戏。')
    local = PROJECT/'.local/steam-backups'; local.mkdir(parents=True, exist_ok=True); local.chmod(0o700)
    manifest = local/'installation.json'
    if args.action == 'install':
        if not core.is_file(): raise RuntimeError('请先完成原生版构建和安装。')
        if old == wanted: print('Steam 启动选项已设置。'); return
        if old: raise RuntimeError('已有非空启动选项，拒绝覆盖；需要先合并现有设置。')
        if manifest.exists(): raise RuntimeError('已有安装记录但配置已变化，请先检查记录。')
        backup = local/('localconfig-' + time.strftime('%Y%m%d-%H%M%S') + '.vdf')
        shutil.copy2(path, backup); backup.chmod(0o600)
        updated = set_option(text, wanted)
        assert read_option(updated) == wanted
        record = {'config':str(path), 'previous':old, 'installed':wanted, 'backup':str(backup), 'before_sha256':hashlib.sha256(text.encode()).hexdigest()}
        manifest.write_text(json.dumps(record, indent=2)); manifest.chmod(0o600)
        atomic_write(path, updated)
        print('已设置 Steam 直接加载 Mod；原启动选项已备份。')
    else:
        record = json.loads(manifest.read_text())
        if record['config'] != str(path) or old != record['installed']: raise RuntimeError('当前配置与安装记录不符，拒绝覆盖。')
        updated = set_option(text, record['previous'])
        atomic_write(path, updated)
        manifest.rename(local/('restored-' + time.strftime('%Y%m%d-%H%M%S') + '.json'))
        print('已恢复原 Steam 启动选项；未改动其他配置。')

if __name__ == '__main__':
    try: main()
    except (RuntimeError, ValueError, OSError) as e: raise SystemExit(str(e))
