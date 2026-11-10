import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import TextField from '@mui/material/TextField';
import Select from '@mui/material/Select';
import MenuItem from '@mui/material/MenuItem';
import ToggleButton from '@mui/material/ToggleButton';
import ToggleButtonGroup from '@mui/material/ToggleButtonGroup';
import { cronAciklama, cronCoz, cronKur, GUN_ADLARI, type CronMod } from './cronMetin';

/** Zamanlama seçici (test turu M22/M23): çıplak cron kutusu yerine anlaşılır seçim —
 * mod (Elle/Saatlik/Günlük/Haftalık/Özel) + saat/dakika/gün girişleri + Türkçe özet satırı.
 * İçe Alım İşleri ve Zamanlanmış Raporlar paylaşır; değer yine cron metnidir ('' = elle). */
export function CronSecici({ deger, degisti }: { deger: string; degisti: (cron: string) => void }) {
  const s = cronCoz(deger);
  const kur = (d: Partial<typeof s>) => degisti(cronKur({ ...s, ...d }));
  const saatDeger = `${String(s.saat).padStart(2, '0')}:${String(s.dakika).padStart(2, '0')}`;
  const saatDegisti = (v: string) => {
    const [h, m] = v.split(':').map(Number);
    if (!Number.isNaN(h) && !Number.isNaN(m)) kur({ saat: h, dakika: m });
  };
  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1 }}>
      <ToggleButtonGroup exclusive size="small" fullWidth value={s.mod}
        onChange={(_, v: CronMod | null) => {
          if (v === null) return;
          // Özel'e geçişte mevcut cron korunur; diğer modlar kendi desenini üretir.
          kur(v === 'Ozel' ? { mod: v, ozel: deger || '0 3 * * *' } : { mod: v });
        }}>
        <ToggleButton value="Elle">Elle</ToggleButton>
        <ToggleButton value="Saatlik">Saatlik</ToggleButton>
        <ToggleButton value="Gunluk">Günlük</ToggleButton>
        <ToggleButton value="Haftalik">Haftalık</ToggleButton>
        <ToggleButton value="Ozel">Özel</ToggleButton>
      </ToggleButtonGroup>

      {s.mod === 'Saatlik' && (
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
          <Typography sx={{ fontSize: 12.5 }} color="text.secondary">Her saatin</Typography>
          <TextField size="small" type="number" value={s.dakika} sx={{ width: 84 }}
            slotProps={{ htmlInput: { min: 0, max: 59 } }}
            onChange={(e) => kur({ dakika: Math.min(59, Math.max(0, Number(e.target.value) || 0)) })} />
          <Typography sx={{ fontSize: 12.5 }} color="text.secondary">. dakikasında</Typography>
        </Box>
      )}
      {s.mod === 'Gunluk' && (
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
          <Typography sx={{ fontSize: 12.5 }} color="text.secondary">Her gün saat</Typography>
          <TextField size="small" type="time" value={saatDeger} sx={{ width: 130 }}
            onChange={(e) => saatDegisti(e.target.value)} />
        </Box>
      )}
      {s.mod === 'Haftalik' && (
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, flexWrap: 'wrap' }}>
          <Typography sx={{ fontSize: 12.5 }} color="text.secondary">Her</Typography>
          <Select size="small" value={s.gun} onChange={(e) => kur({ gun: Number(e.target.value) })} sx={{ minWidth: 130, fontSize: 12.5 }}>
            {GUN_ADLARI.map((ad, i) => <MenuItem key={i} value={i} sx={{ fontSize: 12.5 }}>{ad}</MenuItem>)}
          </Select>
          <Typography sx={{ fontSize: 12.5 }} color="text.secondary">saat</Typography>
          <TextField size="small" type="time" value={saatDeger} sx={{ width: 130 }}
            onChange={(e) => saatDegisti(e.target.value)} />
        </Box>
      )}
      {s.mod === 'Ozel' && (
        <TextField size="small" fullWidth value={deger} placeholder="dk saat ayınGünü ay haftaGünü — ör. */15 * * * *"
          onChange={(e) => degisti(e.target.value)} />
      )}

      {/* Net cevap: bu iş NE ZAMAN çalışacak? */}
      <Typography sx={{ fontSize: 12, fontWeight: 600 }} color="secondary.main">{cronAciklama(deger)}</Typography>
    </Box>
  );
}
