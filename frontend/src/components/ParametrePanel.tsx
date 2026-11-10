// oxlint-disable react/only-export-components -- yardımcı (ilkParametreDegerleri) bileşenle birlikte yaşar
import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import TextField from '@mui/material/TextField';
import MenuItem from '@mui/material/MenuItem';
import Switch from '@mui/material/Switch';
import FormControlLabel from '@mui/material/FormControlLabel';
import type { ReportParameter, ParametreDegerleri } from '../api/queries';
import { dinamikTarihler, dinamikTarihMi, dinamikTarihEtiketi } from '../pages/designer/shared';

type Aralik = { baslangic?: string; bitis?: string };

/** Parametrelerin varsayılanlarından başlangıç değer sözlüğü üretir (panel/çalıştırma bunu tohumlar). */
export function ilkParametreDegerleri(parametreler: ReportParameter[]): ParametreDegerleri {
  const d: ParametreDegerleri = {};
  for (const p of parametreler) {
    if (p.varsayilan !== undefined && p.varsayilan !== null) d[p.anahtar] = p.varsayilan;
    else if (p.tip === 'TarihAralik') d[p.anahtar] = { baslangic: '', bitis: '' };
    else if (p.tip === 'EvetHayir') d[p.anahtar] = false;
    else d[p.anahtar] = '';
  }
  return d;
}

/** Rapor parametreleri için değer girişi (tarih/aralık/sayı/metin/evet-hayır/liste). Çalıştırma-anı;
 * görüntüleyici üst şeridi ve tasarımcı "Çalıştır" önizlemesi paylaşır. */
export function ParametrePanel({ parametreler, degerler, onChange }: {
  parametreler: ReportParameter[];
  degerler: ParametreDegerleri;
  onChange: (d: ParametreDegerleri) => void;
}) {
  const set = (anahtar: string, v: unknown) => onChange({ ...degerler, [anahtar]: v });
  const setAralik = (anahtar: string, uc: 'baslangic' | 'bitis', v: string) => {
    const mevcut = (degerler[anahtar] as Aralik | undefined) ?? {};
    set(anahtar, { ...mevcut, [uc]: v });
  };

  /** Y5 — kayan tarih seçici: sabit takvim değeri yerine "her çalıştırmada hesaplanan" bir uç
   * seçtirir (bugün · ayın başı · bugün − 30 gün …). Boş seçim sabit değere döndürür. */
  const kayanSecici = (deger: string, uygula: (d: string) => void) => (
    <TextField select size="small" value={dinamikTarihMi(deger) ? deger : ''}
      onChange={(e) => uygula(e.target.value)}
      title="Kayan tarih: her çalıştırmada o günün tarihine göre hesaplanır"
      slotProps={{ select: { displayEmpty: true, renderValue: () => '🗓' } }}
      sx={{ width: 62, '& .MuiSelect-select': { py: 0.55, fontSize: 12 } }}>
      <MenuItem value="" sx={{ fontSize: 12 }}><em>sabit tarih</em></MenuItem>
      {dinamikTarihler.map((d) => <MenuItem key={d.deger} value={d.deger} sx={{ fontSize: 12 }}>{d.etiket}</MenuItem>)}
    </TextField>
  );

  function alan(p: ReportParameter) {
    const v = degerler[p.anahtar];
    switch (p.tip) {
      case 'Tarih':
        // Y5: tek tarihte de KAYAN değer seçilebilir (ör. "bu ayın başı") — her çalıştırmada
        // o günün tarihine göre hesaplanır; sabit tarih isteyen takvimden seçer.
        return (
          <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5 }}>
            {dinamikTarihMi(typeof v === 'string' ? v : '')
              ? <TextField size="small" disabled sx={{ width: 165 }}
                  value={`🗓 ${dinamikTarihEtiketi(v as string)}`} />
              : <TextField size="small" type="date" value={typeof v === 'string' ? v : ''}
                  onChange={(e) => set(p.anahtar, e.target.value)} sx={{ width: 165 }} />}
            {kayanSecici(typeof v === 'string' ? v : '', (d) => set(p.anahtar, d))}
          </Box>
        );
      case 'TarihAralik': {
        const r = (v as Aralik | undefined) ?? {};
        return (
          <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5, flexWrap: 'wrap' }}>
            {dinamikTarihMi(r.baslangic ?? '')
              ? <TextField size="small" disabled sx={{ width: 150 }} value={`🗓 ${dinamikTarihEtiketi(r.baslangic!)}`} />
              : <TextField size="small" type="date" value={r.baslangic ?? ''} onChange={(e) => setAralik(p.anahtar, 'baslangic', e.target.value)} sx={{ width: 150 }} />}
            {kayanSecici(r.baslangic ?? '', (d) => setAralik(p.anahtar, 'baslangic', d))}
            <Typography sx={{ fontSize: 12 }} color="text.disabled">–</Typography>
            {dinamikTarihMi(r.bitis ?? '')
              ? <TextField size="small" disabled sx={{ width: 150 }} value={`🗓 ${dinamikTarihEtiketi(r.bitis!)}`} />
              : <TextField size="small" type="date" value={r.bitis ?? ''} onChange={(e) => setAralik(p.anahtar, 'bitis', e.target.value)} sx={{ width: 150 }} />}
            {kayanSecici(r.bitis ?? '', (d) => setAralik(p.anahtar, 'bitis', d))}
          </Box>
        );
      }
      case 'Sayi':
        return <TextField size="small" type="number" value={typeof v === 'number' || typeof v === 'string' ? String(v) : ''}
          onChange={(e) => set(p.anahtar, e.target.value)} sx={{ width: 130 }} />;
      case 'EvetHayir':
        return <FormControlLabel sx={{ m: 0 }} control={<Switch size="small" checked={v === true}
          onChange={(e) => set(p.anahtar, e.target.checked)} />} label={<Typography sx={{ fontSize: 12.5 }}>{v === true ? 'Evet' : 'Hayır'}</Typography>} />;
      case 'Liste':
        return (
          <TextField size="small" select value={v == null ? '' : String(v)} onChange={(e) => set(p.anahtar, e.target.value)} sx={{ minWidth: 150 }}>
            {(p.secenekler ?? []).map((s, i) => <MenuItem key={i} value={String(s.deger)}>{s.etiket}</MenuItem>)}
          </TextField>
        );
      default:
        return <TextField size="small" value={typeof v === 'string' ? v : ''} onChange={(e) => set(p.anahtar, e.target.value)} sx={{ width: 170 }} />;
    }
  }

  return (
    <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 1.75, alignItems: 'flex-end' }}>
      {parametreler.map((p) => (
        <Box key={p.anahtar} sx={{ display: 'flex', flexDirection: 'column', gap: 0.35 }}>
          <Typography sx={{ fontSize: 11.5, fontWeight: 600 }} color="text.secondary">
            {p.etiket}{p.zorunlu ? ' *' : ''}
          </Typography>
          {alan(p)}
        </Box>
      ))}
    </Box>
  );
}
