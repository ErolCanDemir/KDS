import { useEffect, useState, type ReactNode } from 'react';
import { kurulumDurumu, type KurulumDurum } from './api/kurulum';
import { KurulumPage } from './pages/KurulumPage';
import { LoadingCurtain } from './components/Loading';

/** S16/A2 — açılış kapısı: uygulama KURULMAMIŞSA kimlik sağlayıcıya hiç gitmeden (oturum
 * denemeden) kurulum sihirbazını gösterir. Kurulu sistemde tek bir hafif GET maliyeti vardır.
 * Durum okunamazsa (API kapalı/ağ) uygulama NORMAL akışına devam eder — geçici bir ağ hatası
 * yüzünden kullanıcıyı kurulum ekranına düşürmek yanlış olurdu. */
export function KurulumKapisi({ children }: { children: ReactNode }) {
  const [durum, setDurum] = useState<KurulumDurum | null>(null);
  const [bitti, setBitti] = useState(false);

  useEffect(() => {
    let iptal = false;
    kurulumDurumu()
      .then((d) => { if (!iptal) setDurum(d); })
      .catch(() => { if (!iptal) setDurum(null); })
      .finally(() => { if (!iptal) setBitti(true); });
    return () => { iptal = true; };
  }, []);

  if (!bitti) return <LoadingCurtain mesaj="Başlatılıyor…" />;
  if (durum && !durum.yapilandirildi) return <KurulumPage anahtarDosyasi={durum.anahtarDosyasi} />;
  return <>{children}</>;
}
