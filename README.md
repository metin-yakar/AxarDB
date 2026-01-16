# 🔓 UnlockDB - The JavaScript-Native NoSQL Database

![UnlockDB Logo](wwwroot/unlockerdbLogo.png)

[![License: Custom](https://img.shields.io/badge/License-Metin_YAKAR-blue.svg)](LICENSE)
[![Docker](https://img.shields.io/badge/Docker-Ready-2496ED.svg?logo=docker&logoColor=white)](Dockerfile)
[![Built With .NET 8](https://img.shields.io/badge/Built_With-.NET_8.0-512BD4.svg?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![JavaScript Engine](https://img.shields.io/badge/Engine-Jint-f7df1e.svg?logo=javascript&logoColor=black)](https://github.com/sebastienros/jint)

> **UnlockDB** is a high-performance, in-memory NoSQL database server that allows you to write database queries directly in **JavaScript**. Built on ASP.NET Core 8.0, it combines the flexibility of a document store with the power of a full JavaScript runtime.

<br>
![unlockdb1](https://raw.githubusercontent.com/metin-yakar/UnlockDB/refs/heads/main/unlockdb1.gif)
</br>

---

## 🌍 Languages

| [English](README.md) | [Türkçe](Docs/README.tr.md) | [Русский](Docs/README.ru.md) | [中文](Docs/README.zh.md) | [Deutsch](Docs/README.de.md) | [日本語](Docs/README.ja.md) | [العربية](Docs/README.ar.md) | [Nederlands](Docs/README.nl.md) | [Български](Docs/README.bg.md) | [Italiano](Docs/README.it.md) | [Español](Docs/README.es.md) |
|---|---|---|---|---|---|---|---|---|---|---|

---

## 🚀 Key Features

| Feature | Description |
|:---|:---|
| **📜 JavaScript Querying** | Use full JavaScript syntax for queries. `db.users.findall(x => x.age > 18)` |
| **⚡ High Performance** | In-memory storage with `ConcurrentDictionary` and lazy evaluation using LINQ. |
| **🔍 Smart Indexing** | Create ASC/DESC indexes on any field. Supports optimized range queries. |
| **🔗 Joins** | Perform complex joins between collections directly in the query: `db.join(users, orders)`. |
| **🛡️ Secure** | Basic Authentication & **Injection Prevention** via parameter binding. |
| **🐋 Docker Ready** | Runs anywhere with a single `docker run` command. |
| **🖥️ Management Console** | Beautiful Web UI with Monaco Editor, Resizable Datagrid, and Dark Mode. |

---

## 🏎️ Performance Benchmarks

Unlike traditional databases that require complex protocols, UnlockDB executes your logic on the server.

```mermaid
pie title UnlockDB vs Traditional (Ops/Sec)
    "UnlockDB (In-Memory)" : 15000
    "Traditional In-Memory DB" : 12000
    "File-Based DB" : 4000
```
*Benchmark run on standard workstation. Actual performance depends on hardware.*

---

## 🐳 Quick Start with Docker

Get up and running in seconds:

```bash
docker run -d -p 5000:5000 -v $(pwd)/data:/app/data --name unlockdb unlockdb:latest
```

Or using `docker-compose`:
```yaml
services:
  unlockdb:
    build: .
    ports: ["5000:5000"]
    volumes: ["./data:/app/data"]
```

---

## 👨‍💻 Developer

**Metin YAKAR**  
*Software Developer & .NET Expert*  
Istanbul, Turkey 🇹🇷

Experience **since 2011** in C# and software architecture. Metin specializes in building high-performance systems and innovative developer tools.

[![LinkedIn](https://img.shields.io/badge/LinkedIn-Connect-0077B5?style=for-the-badge&logo=linkedin)](https://www.linkedin.com/in/metin-yakar/)

---

## 🤝 Support & Contribution

We are looking for contributors to help build the future of UnlockDB!
**Areas we need help with:**
- [ ] Advanced Configuration System
- [ ] Real-time Synchronization
- [ ] Cluster Monitoring Dashboard
- [ ] Client SDKs (Node.js, Python, Go)
- [ ] Data Replication & Sharding

### 💖 Support the Project

If you love this project, consider supporting its development!

| **Buy Me a Coffee** | **Ethereum** |
|:---:|:---:|
| <a href="https://buymeacoffee.com/metinyakar"><img src="./buymecoffie.png" style="width:100px;height:100px;"/></a> | ![QR](https://api.qrserver.com/v1/create-qr-code/?size=100x100&data=0x1245CEB6c9A5485286975eEC1B2F9D496016761C) |

### 📅 Consulting & Training

Need help integrating UnlockDB or want advice on AI-driven development and Code Automation?
**[Book a session on Cal.com](https://cal.com/metin-yakar-dfij9e)**

---

## 📄 License
**Open Source (Restricted)** - You can use, modify, and learn from UnlockDB. However, you are **not allowed** to clone the repository to release a competing standalone database product. See [LICENSE](LICENSE) for details.
