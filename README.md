<p align="center"><img src="https://cdn.jsdelivr.net/gh/netchx/netch/Netch/Resources/Netch.png" width="128" /></p>

<div align="center">

# Netch
A simple proxy client

[![](https://img.shields.io/badge/telegram-group-green?style=flat-square)](https://t.me/netch_group)
[![](https://img.shields.io/badge/telegram-channel-blue?style=flat-square)](https://t.me/netch_channel)
[![](https://img.shields.io/github/downloads/clankoss/Netch/total.svg?style=flat-square)](https://github.com/clankoss/Netch/releases)
[![](https://img.shields.io/github/v/release/clankoss/Netch?style=flat-square)](https://github.com/clankoss/Netch/releases)
</div>

## Features
Some features may not be implemented in version 1

### Modes
- ProcessMode - Use Netfilter driver to intercept process traffic
- ShareMode - Share your network based on WinPcap / Npcap
- TapMode - Use TAP-Windows driver to create virtual adapter
- TunMode - Use WinTUN driver to create virtual adapter
- WebMode - Web proxy mode
- WmpMode - Proxy forwarding (eg. OBS Streaming)

### Protocols
- [Socks5](https://www.wikiwand.com/en/SOCKS)
- [Shadowsocks](https://github.com/shadowsocks/shadowsocks-libev)
- [ShadowsocksR](https://github.com/shadowsocksrr/shadowsocksr-libev)
- [Trojan](https://trojan-gfw.github.io/trojan/)
- [VLess](https://github.com/xtls/xray-core)
- [VMess](https://github.com/v2fly/v2ray-core)

### Others
- UDP NAT FullCone (May limited by your server)
- Windows x64 / .NET 10 WinForms with Per-Monitor V2 DPI scaling

## 2.0.0 · .NET 10 版

基于上游 1.9.0（`d829e34`）。在 Windows x64 安装 .NET 10 SDK 后，从源码目录运行
`powershell -NoProfile -File .\build.ps1`。生成的 `release\Netch.exe` 需要预装 Windows x64 的 .NET 10 Desktop Runtime，使用时保留同目录的应用文件和 `bin` 目录。

- 从剪贴板导入 VLESS、VMess、Trojan 或 Shadowsocks 分享链接。Xray 负责 VLESS、VMess 和 TLS/Trojan，Shadowsocks 2022 由 sing-box 运行。
- 在服务器菜单选择 **Add [sing-box JSON] Server**，可直接粘贴或从文件导入含 `type`、`server` 和 `server_port` 的单个出站 JSON 对象。需要其他出站或完整路由规则的配置请单独在 sing-box 里运行。
- 在 **帮助 → 检查代理内核更新** 查看本地和官方稳定版版本。手动升级时，先停止加速，再从结果中的官方发布页下载 Windows x64 版本并替换 `bin` 下对应的内核。
- 初始模式列表为空，可通过「模式」菜单创建自己的进程或路由模式。选好节点和游戏进程模式后，应用会在下次打开时自动连接。升级时沿用 `data\settings.json` 中保存的选项，可在设置里启用 **Start when opened**。
- 游戏 TCP 加速使用进程模式，匹配游戏的实际联网 EXE。新配置默认只转发 TCP 并关闭 DNS 劫持。旧配置可在 Redirector 设置中把过滤协议设为 TCP。


## Sponsor
<a href="https://www.jetbrains.com/?from=Netch"><img src="https://cdn.jsdelivr.net/gh/netchx/netch/jetbrains.svg" alt="JetBrains" width="200"/></a>

- [NeroCloud](https://nerocloud.io)

## Donate
- XMR *48ju3ELNZEa6wwPBMexCJ9G218BGY2XwhH6B6bmkFuJ3QgM4hPw2Pra35jPtuBZSc7SLNWeBpiWJZWjQeMAiLnTx2tH2Efx*
- ETH *0x23dac0a93bcd71fec7a95833ad030338f167f185*

## Credit
- [WinTUN](https://www.wintun.net)
- [aioCloud](https://github.com/aiocloud)
- [NetFilter](https://netfiltersdk.com)
- [TAP-Windows](https://github.com/OpenVPN/tap-windows6)
- [Shadowsocks](https://github.com/shadowsocks/shadowsocks-libev)
- [ShadowsocksR](https://github.com/shadowsocksrr/shadowsocksr-libev)
- [Trojan](https://github.com/trojan-gfw/trojan)
- [V2Ray](https://github.com/v2fly/v2ray-core)
- [XRay](https://github.com/xtls/xray-core)

- [Packaged Xray source (v26.11.9)](https://github.com/XTLS/Xray-core/tree/v26.11.9)
- [Packaged sing-box source (v1.14.4)](https://github.com/SagerNet/sing-box/tree/v1.14.4)

## GitHub Releases

Windows x64 构建通过 [clankoss/Netch Releases](https://github.com/clankoss/Netch/releases) 分发。发布时先提交 Netch/Netch.csproj 的版本号修改，再推送匹配的标签，如 v2.0.0。工作流会发布 Netch-2.0.0-win-x64.zip 和对应的 SHA-256 文件。

应用内可用「帮助 → 检查应用更新」手动检查稳定版。确认后下载并校验 GitHub 提供的 SHA-256，再退出、更新并重启应用。更新将覆盖发行包中的应用文件，用户配置和模式保存在各自的目录中。发布包依赖 Windows x64 的 .NET 10 Desktop Runtime。
