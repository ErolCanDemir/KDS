import { useEffect, useRef } from 'react';
import Box from '@mui/material/Box';
import Paper from '@mui/material/Paper';
import Typography from '@mui/material/Typography';
import Button from '@mui/material/Button';
import Chip from '@mui/material/Chip';
import TextField from '@mui/material/TextField';
import LinearProgress from '@mui/material/LinearProgress';
import CheckCircleIcon from '@mui/icons-material/CheckCircle';
import RadioButtonUncheckedIcon from '@mui/icons-material/RadioButtonUnchecked';
import AutorenewIcon from '@mui/icons-material/Autorenew';
import ErrorOutlineIcon from '@mui/icons-material/ErrorOutlineOutlined';
import SendIcon from '@mui/icons-material/Send';
import { boxRadius } from '../../theme';
import { KadiRozet } from './KadiRozet';
import type { Mesaj, TakipOnerisi } from './tipler';

/** S31 — SOHBET SÜTUNU: konuşma burada akar, SONUÇ burada çizilmez (tuvale gider).
 * Genişlik bilerek dar: okuma satırı 65-75 karakterde kalsın. */
export function SohbetSutunu({
  mesajlar, seciliId, onSec, bekliyor, gecenSn, onDurdur, soru, onSoru, onGonder, takipler, onTakip, kilit,
}: {
  mesajlar: Mesaj[];
  seciliId: string | null;
  onSec: (id: string) => void;
  bekliyor: boolean;
  gecenSn: number;
  onDurdur: () => void;
  soru: string;
  onSoru: (s: string) => void;
  onGonder: () => void;
  takipler: TakipOnerisi[];
  onTakip: (t: TakipOnerisi) => void;
  kilit: boolean;
}) {
  const sonRef = useRef<HTMLDivElement>(null);
  // Yeni mesaj gelince en alta kay — kullanıcı yukarı kaydırmışken zıplatmamak için yalnız
  // mesaj SAYISI değişince (yeni içerik) tetiklenir.
  useEffect(() => { sonRef.current?.scrollIntoView({ behavior: 'smooth', block: 'end' }); },
    [mesajlar.length, bekliyor]);

  return (
    <Paper variant="outlined" sx={{ borderRadius: boxRadius, display: 'flex', flexDirection: 'column', overflow: 'hidden', minHeight: 0 }}>
      <Box sx={{ px: 1.75, py: 1.25, borderBottom: '1px solid', borderColor: 'divider', display: 'flex', alignItems: 'center', gap: 1 }}>
        <Typography sx={{ fontWeight: 700, fontSize: 13 }}>Sohbet</Typography>
        <Typography sx={{ fontSize: 11.5, opacity: 0.7, ml: 'auto' }}>
          {mesajlar.filter((m) => m.rol === 'kadi').length} cevap
        </Typography>
      </Box>

      <Box sx={{ flex: 1, overflowY: 'auto', p: 1.75, display: 'flex', flexDirection: 'column', gap: 1.5, minHeight: 0 }}>
        {mesajlar.map((m) => (
          <MesajSatiri key={m.id} mesaj={m} secili={m.id === seciliId} onSec={() => onSec(m.id)} />
        ))}

        {bekliyor && <BeklemeKarti gecenSn={gecenSn} onDurdur={onDurdur} />}

        {!bekliyor && takipler.length > 0 && (
          <Box sx={{ display: 'flex', gap: 0.75, flexWrap: 'wrap', pl: 4.5 }}>
            {takipler.map((t) => (
              <Chip key={t.etiket} label={t.etiket} size="small" onClick={() => onTakip(t)}
                data-testid="takip-cip"
                sx={{
                  fontSize: 11.5, cursor: 'pointer', bgcolor: 'var(--kds-tealdolgu)',
                  color: 'secondary.main', fontWeight: 600, '&:hover': { filter: 'brightness(0.96)' },
                }} />
            ))}
          </Box>
        )}
        <div ref={sonRef} />
      </Box>

      <Box sx={{ p: 1.5, borderTop: '1px solid', borderColor: 'divider' }}>
        <Box sx={{ display: 'flex', gap: 1, alignItems: 'flex-end' }}>
          <TextField
            size="small" fullWidth multiline maxRows={4}
            placeholder={mesajlar.length === 0 ? 'Sorunu yaz…' : 'Sorunu yaz ya da düzeltme iste…'}
            value={soru} onChange={(e) => onSoru(e.target.value)}
            onKeyDown={(e) => { if (e.key === 'Enter' && !e.shiftKey) { e.preventDefault(); onGonder(); } }}
            disabled={bekliyor || kilit}
            slotProps={{ htmlInput: { maxLength: 500 } }}
          />
          <Button data-kadi-hedef="soru-sor-btn" variant="contained" onClick={onGonder}
            disabled={bekliyor || kilit || !soru.trim()} sx={{ minWidth: 0, px: 1.5, height: 40 }}>
            <SendIcon fontSize="small" />
          </Button>
        </Box>
        {/* Feragatname BİR KEZ, girdinin yanında (dipnotta okunmuyor) */}
        <Typography sx={{ fontSize: 10.5, opacity: 0.6, mt: 0.6 }}>
          Cevaplar tanımlı metriklere dayanır; yine de rakamı kanıt şeridinden doğrulayın.
        </Typography>
      </Box>
    </Paper>
  );
}

function MesajSatiri({ mesaj, secili, onSec }: { mesaj: Mesaj; secili: boolean; onSec: () => void }) {
  if (mesaj.rol === 'ben') {
    return (
      <Box sx={{ display: 'flex', gap: 1, alignItems: 'flex-start' }}>
        <Box sx={{
          width: 28, height: 28, borderRadius: '50%', bgcolor: 'var(--kds-dolgu)', flex: '0 0 auto',
          display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 11, fontWeight: 700,
          color: 'text.secondary',
        }}>Ben</Box>
        <Box sx={{
          bgcolor: 'var(--kds-dolgu)', px: 1.5, py: 1, borderRadius: '12px 12px 12px 3px', fontSize: 13.5,
        }}>{mesaj.metin}</Box>
      </Box>
    );
  }

  if (mesaj.rol === 'hata') {
    return (
      <Box sx={{ display: 'flex', gap: 1, alignItems: 'flex-start' }}>
        <KadiRozet uzgun />
        <Box sx={{
          flex: 1, px: 1.5, py: 1, borderRadius: '12px 12px 3px 12px', fontSize: 13,
          bgcolor: 'var(--kds-kirmizidolgu)', border: '1px solid', borderColor: 'error.main',
        }}>
          <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.75, mb: 0.5 }}>
            <ErrorOutlineIcon sx={{ fontSize: 16, color: 'error.main' }} />
            <Typography sx={{ fontSize: 12.5, fontWeight: 700, color: 'error.main' }}>Çeviremedim</Typography>
          </Box>
          {mesaj.metin}
        </Box>
      </Box>
    );
  }

  // Kadi cevabı: ÖZET cümle + ölçümler. Grafik/tablo YOK — o tuvalde.
  return (
    <Box sx={{ display: 'flex', gap: 1, alignItems: 'flex-start' }}>
      <KadiRozet />
      <Box onClick={onSec} data-testid="cevap-balonu"
        sx={{
          flex: 1, px: 1.5, py: 1.25, borderRadius: '12px 12px 3px 12px', cursor: 'pointer',
          bgcolor: 'background.paper', border: secili ? '1.5px solid' : '1px solid',
          borderColor: secili ? 'secondary.main' : 'divider',
          '&:hover': { borderColor: 'secondary.main' },
        }}>
        <Typography sx={{ fontSize: 13.5 }}>{mesaj.ozet}</Typography>
        <Typography sx={{ fontSize: 11, opacity: 0.7, mt: 0.6 }}>
          {mesaj.satir} satır · çeviri {(mesaj.ceviriMs / 1000).toFixed(1)} sn · sorgu {mesaj.sorguMs} ms
          {!secili && ' · göstermek için tıkla'}
        </Typography>
      </Box>
    </Box>
  );
}

/** 10+ saniyelik bekleme rejimi: aşama + süre BEKLENTİSİ (sahte yüzde değil) + durdurma.
 *
 * Dürüstlük kuralı (NN/g): adım listesi modelin gerçek hesabını taklit etmemeli. Burada yalnız
 * GERÇEKTEN bilinen şey "tamamlandı" işaretlenir: soru gönderildi (kesin), model düşünüyor (kesin,
 * o an bekliyoruz). Sonraki adımlar SOLUK — iddia değil, sıradaki iş. */
function BeklemeKarti({ gecenSn, onDurdur }: { gecenSn: number; onDurdur: () => void }) {
  const adimlar = [
    { ad: 'Soruyu yerel modele gönderdim', durum: 'bitti' as const },
    { ad: 'Model sorguyu yazıyor', durum: 'suruyor' as const },
    { ad: 'Sorgu veritabanında çalışacak', durum: 'bekliyor' as const },
    { ad: 'Sonucu tuvale çizeceğim', durum: 'bekliyor' as const },
  ];
  return (
    <Box sx={{ display: 'flex', gap: 1, alignItems: 'flex-start' }} data-testid="bekleme-karti">
      <KadiRozet dusunuyor />
      <Box sx={{
        flex: 1, px: 1.5, py: 1.25, borderRadius: '12px 12px 3px 12px',
        bgcolor: 'var(--kds-zemin2)', border: '1px solid', borderColor: 'divider',
      }}>
        {adimlar.map((a) => (
          <Box key={a.ad} sx={{ display: 'flex', alignItems: 'center', gap: 0.75, mb: 0.4 }}>
            {a.durum === 'bitti' && <CheckCircleIcon sx={{ fontSize: 14, color: 'success.main' }} />}
            {a.durum === 'suruyor' && (
              <AutorenewIcon sx={{ fontSize: 14, color: 'secondary.main', animation: 'kds-donen 1.4s linear infinite' }} />
            )}
            {a.durum === 'bekliyor' && <RadioButtonUncheckedIcon sx={{ fontSize: 14, color: 'text.disabled' }} />}
            <Typography sx={{
              fontSize: 12.5,
              color: a.durum === 'bekliyor' ? 'text.disabled' : a.durum === 'suruyor' ? 'secondary.main' : 'success.main',
              fontWeight: a.durum === 'suruyor' ? 700 : 400,
            }}>{a.ad}</Typography>
          </Box>
        ))}
        <LinearProgress sx={{ my: 1, height: 4, borderRadius: 99 }} />
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, flexWrap: 'wrap' }}>
          <Typography sx={{ fontSize: 11.5, opacity: 0.75 }}>
            {gecenSn} sn · genelde 20–40 sn sürer
          </Typography>
          <Button size="small" onClick={onDurdur} sx={{ ml: 'auto', fontSize: 11.5, py: 0.2 }}>Durdur</Button>
        </Box>
      </Box>
    </Box>
  );
}
