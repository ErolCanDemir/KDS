import { useEffect, useSyncExternalStore } from 'react';
import Box from '@mui/material/Box';
import CircularProgress from '@mui/material/CircularProgress';
import Typography from '@mui/material/Typography';
import { boxRadius } from '../theme';
import { kadiAktif, kadiAktifAbone, kadiYukleniyor } from '../kadi/kadiKanal';
import { Kadi3D } from '../kadi/Kadi3D';

/* S28b — TÜM yükleme perdeleri artık KADİ'dir: ekran hafifçe kararır, Kadi ortaya süzülüp
 * mesajı balonuyla söyler (yüklemede düşünür, sorgu koşarken koşar); bitince evine döner.
 * Kadi henüz montajlı değilse (kurulum sihirbazı / kimlik kapısı) eski spinner görünümü kalır. */

/** Kadi sahnesini açar; PERDEYİ ARTIK KADİ'NİN KENDİSİ çizer (24 Ağu 2026) — burada ikinci bir
 * perde koymak çift karartma yapardı. Sayfalar kadiYukleniyor'u doğrudan çağırdığında da aynı
 * sahne (perde + merkez + balon) çıksın diye sorumluluk kanala taşındı. */
function KadiSahnesi({ mesaj, poz, karartma }: { mesaj: string; poz: 'dusun' | 'kos'; karartma: 'yogun' | 'hafif' }) {
  useEffect(() => kadiYukleniyor(mesaj, poz, karartma), [mesaj, poz, karartma]);
  return null;
}

/** Sistem geneli yükleme perdesi: Kadi ekranın ortasına gelir, mesajı söyler (düşünme pozunda).
 * Sayfa ilk yüklemelerinde TEK standart budur; işlem sırasında <RunningIndicator/> kullanılır. */
export function LoadingCurtain({ mesaj = 'Yükleniyor…' }: { mesaj?: string }) {
  const kadiVar = useSyncExternalStore(kadiAktifAbone, kadiAktif);
  if (kadiVar) return <KadiSahnesi mesaj={mesaj} poz="dusun" karartma="yogun" />;
  return (
    <Box sx={{
      position: 'fixed', inset: 0, zIndex: (t) => t.zIndex.modal + 1,
      display: 'flex', alignItems: 'center', justifyContent: 'center',
      bgcolor: 'rgba(15,45,78,.28)', backdropFilter: 'blur(2px)',
      animation: 'perdeGirisi .18s ease-out',
      '@keyframes perdeGirisi': { from: { opacity: 0 }, to: { opacity: 1 } },
    }}>
      <Box sx={{
        display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 1.25,
        bgcolor: 'background.paper', borderRadius: boxRadius, px: 4, py: 3, minWidth: 200,
        boxShadow: '0 12px 40px rgba(15,45,78,.25)',
      }}>
        {/* S43 — KADİ BURADA DA VAR (kullanıcı isteği 26 Ağu 2026: "giriş ekranındaki yükleniyorda
            da Kadi olsun"). Bu dal, Kadi'nin KANALI henüz yokken çalışır: oturum doğrulama ve
            açılış perdeleri AppShell'den ÖNCE çizilir, dolayısıyla KadiYoldas montajlı değildir.
            Bu yüzden burada tam Kadi değil, onun 3B gövdesi doğrudan çizilir — hareket/balon yok,
            ama kullanıcı ürünün ilk anında bile aynı yüzü görür. */}
        <Box sx={{ width: 96 }}>
          <Kadi3D durum="dusun" mini={false} />
        </Box>
        <Typography sx={{ fontSize: 13, fontWeight: 600, color: 'primary.main' }}>{mesaj}</Typography>
      </Box>
    </Box>
  );
}

/** İşlem göstergesi (sorgu çalışıyor vb.): Kadi ortaya gelir ve KOŞARAK bekler — içerik çok
 * hafif kararmış perdenin altında görünür kalır, ne beklendiği balonda okunur. */
export function RunningIndicator({ mesaj = 'Sorguyu çalıştırıyorum…' }: { mesaj?: string }) {
  const kadiVar = useSyncExternalStore(kadiAktifAbone, kadiAktif);
  if (kadiVar) return <KadiSahnesi mesaj={mesaj} poz="kos" karartma="hafif" />;
  return (
    <Box sx={{
      position: 'fixed', inset: 0, zIndex: (t) => t.zIndex.modal + 1,
      bgcolor: 'rgba(15,45,78,.08)',
      animation: 'perdeGirisi .15s ease-out',
      '@keyframes perdeGirisi': { from: { opacity: 0 }, to: { opacity: 1 } },
    }}>
      <Box sx={{
        position: 'absolute', bottom: 34, left: '50%',
        display: 'flex', alignItems: 'center', gap: 1.25,
        bgcolor: 'background.paper', borderRadius: 999, pl: 1.25, pr: 2.25, py: 1,
        border: '1px solid rgba(14,111,122,.25)',
        boxShadow: '0 10px 30px rgba(15,45,78,.28)',
        animation: 'hapGirisi .22s ease-out',
        '@keyframes hapGirisi': {
          from: { opacity: 0, transform: 'translate(-50%, 14px)' },
          to: { opacity: 1, transform: 'translate(-50%, 0)' },
        },
        transform: 'translate(-50%, 0)',
      }}>
        <CircularProgress size={22} thickness={4.4} color="secondary" disableShrink />
        <Typography sx={{ fontSize: 13, fontWeight: 600, color: 'primary.main', whiteSpace: 'nowrap' }}>{mesaj}</Typography>
      </Box>
    </Box>
  );
}
