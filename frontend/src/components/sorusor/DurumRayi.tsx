import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import Chip from '@mui/material/Chip';
import Tooltip from '@mui/material/Tooltip';
import type { DurumCipi, SorguDurumu } from '../../api/soru';

/** Çip türüne göre renk: kırılım/hesap/filtre/sıralama/limit gözle ayrılsın (renk TEK ayırt edici
 * değil — etiket metni zaten türü söyler; renk yalnız tarama hızını artırır). */
const TUR_STILI: Record<string, { renk: string; dolgu: string }> = {
  metrik: { renk: 'var(--kds-teal-metin, #0e6f7a)', dolgu: 'var(--kds-tealdolgu)' },
  hesap: { renk: 'text.primary', dolgu: 'var(--kds-dolgu)' },
  kirilim: { renk: 'text.primary', dolgu: 'var(--kds-mavidolgu)' },
  filtre: { renk: 'text.primary', dolgu: 'var(--kds-mordolgu)' },
  siralama: { renk: 'text.primary', dolgu: 'var(--kds-dolgu)' },
  limit: { renk: 'text.primary', dolgu: 'var(--kds-amberdolgu)' },
};

const TUR_ACIKLAMA: Record<string, string> = {
  metrik: 'Katalogdaki metrik tanımı — sorgunun kendisi, kaldırılamaz',
  hesap: 'Hesaplanan ölçü — sorgunun kendisi, kaldırılamaz',
  kirilim: 'Kırılım (gruplama) — kaldırırsan toplam tek satıra iner',
  filtre: 'Filtre — kaldırırsan tüm satırlar gelir',
  siralama: 'Sıralama',
  limit: 'Satır limiti — kaldırırsan tümü gelir',
};

/** S31 — DURUM RAYI: sorgunun o anki hâli tuvalin üstünde sürekli görünür.
 *
 * Gerekçe (tasarım araştırması, 23 Ağu 2026): sohbet arayüzlerinde aktif filtre/limit/kırılım
 * mesajlara dağılınca kullanıcı "unutulmuş filtre altında" yanlış çıkarım yapıyor — çalışma belleği
 * ~4 parça ile sınırlı. Durumu sürekli göstermek bu hatanın doğrudan panzehiri; ayrıca düzeltme
 * özelliğini KEŞFEDİLİR kılar: çipi silmek = "bu kısıtı kaldır" demek. */
export function DurumRayi({ durum, onKaldir }: {
  durum: SorguDurumu;
  onKaldir?: (cip: DurumCipi) => void;
}) {
  if (durum.cipler.length === 0) return null;
  return (
    <Box sx={{
      px: 1.75, py: 1, borderBottom: '1px solid', borderColor: 'divider',
      bgcolor: 'var(--kds-zemin2)', display: 'flex', alignItems: 'center', gap: 0.75, flexWrap: 'wrap',
    }}>
      <Typography sx={{ fontSize: 12, fontWeight: 700, mr: 0.25 }}>Sorgunun hâli</Typography>
      <Chip size="small" label={durum.haritaAd} variant="outlined" sx={{ fontSize: 11.5, height: 24 }} />
      {durum.cipler.map((c, i) => {
        const stil = TUR_STILI[c.tur] ?? TUR_STILI.siralama;
        const kaldir = c.kaldirilabilir && onKaldir ? () => onKaldir(c) : undefined;
        return (
          <Tooltip key={`${c.tur}-${c.etiket}-${i}`} title={TUR_ACIKLAMA[c.tur] ?? ''}>
            <Chip
              size="small"
              label={c.etiket}
              data-testid={`durum-cip-${c.tur}`}
              /* Çipin GÖVDESİ de kaldırır, yalnız × ikonu değil: canlı turda küçük ikonu ıskalayıp
               * "tıkladım, hiçbir şey olmadı" yaşandı (23 Ağu 2026). Tooltip ne olacağını söyler. */
              onClick={kaldir}
              onDelete={kaldir}
              sx={{
                fontSize: 11.5, height: 24, bgcolor: stil.dolgu, color: stil.renk, fontWeight: 600,
                cursor: kaldir ? 'pointer' : 'default',
                '&:hover': kaldir ? { filter: 'brightness(0.94)' } : undefined,
              }}
            />
          </Tooltip>
        );
      })}
    </Box>
  );
}
