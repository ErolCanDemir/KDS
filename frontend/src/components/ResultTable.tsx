import { useEffect, useMemo, useState } from 'react';
import Box from '@mui/material/Box';
import MenuItem from '@mui/material/MenuItem';
import Pagination from '@mui/material/Pagination';
import Select from '@mui/material/Select';
import Typography from '@mui/material/Typography';
import type { QueryRunResult } from '../api/queries';

const sayfaBoylari = [10, 25, 50, 100];

/** Hücre değerini insan-okur biçime çevirir: sayılar tr-TR binlik ayraçlı, ISO tarihler
 * gg.aa.yyyy (saat varsa + SS:dd), bit ✓/—, NULL soluk "—". */
function hucre(v: unknown): { metin: string; soluk?: boolean; renk?: string } {
  if (v === null || v === undefined) return { metin: '—', soluk: true };
  if (typeof v === 'number') return { metin: v.toLocaleString('tr-TR', { maximumFractionDigits: 2 }) };
  if (typeof v === 'boolean') return v ? { metin: '✓', renk: 'success.main' } : { metin: '✗', soluk: true };
  const s = String(v);
  const tarihSaat = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})(?::(\d{2}))?/.exec(s);
  if (tarihSaat) {
    const [, y, a, g, sa, dk] = tarihSaat;
    const saat = sa === '00' && dk === '00' ? '' : ` ${sa}:${dk}`;
    return { metin: `${g}.${a}.${y}${saat}` };
  }
  const tarih = /^(\d{4})-(\d{2})-(\d{2})$/.exec(s);
  if (tarih) return { metin: `${tarih[3]}.${tarih[2]}.${tarih[1]}` };
  return { metin: s };
}

/** Sorgu sonucu grid'i — Sorgu Tasarımcısı, SQL Editörü ve Rapor görüntüleyici paylaşır.
 * İstemci tarafı sayfalama (varsayılan 25 satır), yapışkan başlık, zebra + hover, tr-TR
 * sayı/tarih biçimlendirme. Hizalama KOLON bazlıdır (sayısal kolonda başlık da sağda).
 * D3 (bilinçli sınır): sonuç kümesi İSTEMCİDE tutulur — yürütücünün sert tavanı 10.000 satırdır
 * ve bu boyut bellekte sorunsuz; sunucu-taraflı sayfalama ancak tavan artarsa gündeme gelir. */
export function ResultTable({ sonuc }: { sonuc: QueryRunResult }) {
  const [sayfa, setSayfa] = useState(0);
  const [boy, setBoy] = useState(25);
  // M28: kolon sıralaması — başlığa tık asc → desc → kapalı döngüsü. Tüm sonuç kümesi
  // istemcide olduğundan sıralama TAM veri üstünde yapılır (yalnız görünen sayfa değil).
  const [siralama, setSiralama] = useState<{ ci: number; azalan: boolean } | null>(null);
  useEffect(() => { setSayfa(0); setSiralama(null); }, [sonuc]); // yeni sonuç → başa dön

  const sagaYasli = useMemo(
    () => sonuc.kolonlar.map((_, ci) => sonuc.satirlar.some((r) => typeof r[ci] === 'number')),
    [sonuc]);

  const sirali = useMemo(() => {
    if (!siralama) return sonuc.satirlar;
    const { ci, azalan } = siralama;
    const yon = azalan ? -1 : 1;
    return [...sonuc.satirlar].sort((a, b) => {
      const x = a[ci]; const y = b[ci];
      if (x === null || x === undefined) return y === null || y === undefined ? 0 : 1; // NULL hep sonda
      if (y === null || y === undefined) return -1;
      if (typeof x === 'number' && typeof y === 'number') return (x - y) * yon;
      if (typeof x === 'boolean' && typeof y === 'boolean') return ((x ? 1 : 0) - (y ? 1 : 0)) * yon;
      return String(x).localeCompare(String(y), 'tr') * yon;
    });
  }, [sonuc, siralama]);

  const basligaTikla = (ci: number) => {
    setSayfa(0);
    setSiralama((s) => (s?.ci !== ci ? { ci, azalan: false } : s.azalan ? null : { ci, azalan: true }));
  };

  const toplam = sonuc.satirlar.length;
  const sayfaSayisi = Math.max(1, Math.ceil(toplam / boy));
  const etkinSayfa = Math.min(sayfa, sayfaSayisi - 1);
  const bas = etkinSayfa * boy;
  const gorunen = sirali.slice(bas, bas + boy);

  return (
    <Box>
      <Box sx={{ overflow: 'auto', maxHeight: 560, bgcolor: 'background.paper' }}>
        <Box component="table" sx={{ width: '100%', borderCollapse: 'separate', borderSpacing: 0, fontSize: 12.5 }}>
          <Box component="thead">
            <Box component="tr">
              {sonuc.kolonlar.map((k, ci) => (
                <Box component="th" key={k.ad} title={`${k.veriTipi} — sıralamak için tıkla`}
                  onClick={() => basligaTikla(ci)} sx={{
                  position: 'sticky', top: 0, zIndex: 1,
                  textAlign: sagaYasli[ci] ? 'right' : 'left',
                  px: 1.5, py: 1, cursor: 'pointer', userSelect: 'none',
                  /* tema token'ları — sabit açık hex koyu modda başlığı bembeyaz bırakıyordu */
                  bgcolor: siralama?.ci === ci ? 'var(--kds-dolgu)' : 'var(--kds-zemin2)', color: 'primary.main',
                  borderBottom: '2px solid', borderRight: '1px solid', borderColor: 'divider',
                  '&:last-of-type': { borderRight: 'none' },
                  fontSize: 10.5, fontWeight: 800, textTransform: 'uppercase', letterSpacing: 0.4,
                  whiteSpace: 'nowrap', '&:hover': { bgcolor: 'var(--kds-dolgu)' },
                }}>{k.ad}{siralama?.ci === ci ? (siralama.azalan ? ' ▼' : ' ▲') : ''}</Box>
              ))}
            </Box>
          </Box>
          <Box component="tbody" sx={{
            '& tr:nth-of-type(even) td': { bgcolor: 'var(--kds-zemin2)' },
            '& tr:hover td': { bgcolor: 'var(--kds-tealdolgu)' },
          }}>
            {gorunen.map((r, ri) => (
              <Box component="tr" key={bas + ri}>
                {r.map((v, ci) => {
                  const h = hucre(v);
                  return (
                    <Box component="td" key={ci} sx={{
                      px: 1.5, py: 0.7,
                      borderBottom: '1px solid', borderRight: '1px solid', borderColor: 'divider',
                      '&:last-of-type': { borderRight: 'none' },
                      fontVariantNumeric: 'tabular-nums', whiteSpace: 'nowrap',
                      textAlign: sagaYasli[ci] ? 'right' : 'left',
                      color: h.renk ?? (h.soluk ? 'text.disabled' : 'inherit'),
                      transition: 'background-color .08s',
                    }}>{h.metin}</Box>
                  );
                })}
              </Box>
            ))}
            {toplam === 0 && (
              <Box component="tr">
                <Box component="td" colSpan={sonuc.kolonlar.length}
                  sx={{ px: 1.5, py: 3, textAlign: 'center', color: 'text.disabled', fontSize: 12.5 }}>
                  Sonuç boş — filtreleri gevşetmeyi dene.
                </Box>
              </Box>
            )}
          </Box>
        </Box>
      </Box>

      {/* alt çubuk: özet + sayfalama */}
      <Box sx={{
        display: 'flex', alignItems: 'center', gap: 1.5, flexWrap: 'wrap',
        px: 1.5, py: 0.7, borderTop: '1px solid', borderColor: 'divider', bgcolor: 'var(--kds-zemin2)',
      }}>
        <Typography sx={{ fontSize: 11.5 }} color="text.secondary">
          <b>{toplam.toLocaleString('tr-TR')}</b> satır · {sonuc.sureMs.toLocaleString('tr-TR')} ms
          {sonuc.satirSiniriUygulandi ? ' · satır sınırına ulaşıldı (fazlası var)' : ''}
        </Typography>
        <Box sx={{ flexGrow: 1 }} />
        <Typography sx={{ fontSize: 11.5 }} color="text.secondary">Sayfa başına</Typography>
        <Select size="small" value={boy}
          onChange={(e) => { setBoy(Number(e.target.value)); setSayfa(0); }}
          sx={{ fontSize: 12, '& .MuiSelect-select': { py: 0.3 } }}>
          {sayfaBoylari.map((b) => <MenuItem key={b} value={b} sx={{ fontSize: 12.5 }}>{b}</MenuItem>)}
        </Select>
        <Typography sx={{ fontSize: 11.5, minWidth: 92, textAlign: 'center' }} color="text.secondary">
          {toplam === 0 ? '0' : `${(bas + 1).toLocaleString('tr-TR')}–${Math.min(bas + boy, toplam).toLocaleString('tr-TR')}`} / {toplam.toLocaleString('tr-TR')}
        </Typography>
        {/* M27: ok yerine sayfa numaraları */}
        <Pagination size="small" count={sayfaSayisi} page={etkinSayfa + 1}
          onChange={(_, p) => setSayfa(p - 1)} siblingCount={1} boundaryCount={1}
          sx={{ '& .MuiPaginationItem-root': { fontSize: 11.5, minWidth: 24, height: 24 } }} />
      </Box>
    </Box>
  );
}
