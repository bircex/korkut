# vendor/ — Referans kaynak (repoya alınmaz)

Bu klasördeki üçüncü-taraf kodlar `.gitignore` ile repo dışında tutulur. İçtihat/mevzuat
**tool**'larını (`tools/ictihat_ara`, `tools/mevzuat_ara`, `tools/karar_metin`) bunların
httpx istemcilerinden uyarlıyoruz (bkz. KAYNAKLAR.md §8, MIMARI.md §12). MIT — alınan
dosyalarda telif notu korunur.

## Yeniden elde etme
```bash
mkdir -p vendor && cd vendor
git clone https://github.com/saidsurucu/yargi-mcp.git
git clone https://github.com/saidsurucu/mevzuat-mcp.git
```

## Sabitlenen sürümler (uyarlamanın yapıldığı commit'ler)
| Repo | Commit | Tarih | Lisans |
|------|--------|-------|--------|
| saidsurucu/yargi-mcp | `6bbc656dc6143208201bec5edda07da322cd67ee` | 2026-06-18 | MIT |
| saidsurucu/mevzuat-mcp | `5671233b3e77ea82d11cf35a22af1985c27bf892` | 2026-06-18 | MIT |

> Önemli: Adamın barındırdığı uzak MCP uçlarını (surucu.dev) KULLANMA — kendi sunucumuzda
> çalıştırırız (gizlilik). Veri erişimi token gerektirmez (düz httpx).
