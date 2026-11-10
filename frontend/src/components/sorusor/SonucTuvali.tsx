import { useEffect, useMemo, useRef, useState } from 'react';
import Box from '@mui/material/Box';
import Paper from '@mui/material/Paper';
import Typography from '@mui/material/Typography';
import Button from '@mui/material/Button';
import Tooltip from '@mui/material/Tooltip';
import Collapse from '@mui/material/Collapse';
import Tabs from '@mui/material/Tabs';
import Tab from '@mui/material/Tab';
import SaveOutlinedIcon from '@mui/icons-material/SaveOutlined';
import ExpandMoreIcon from '@mui/icons-material/ExpandMore';
import VerifiedOutlinedIcon from '@mui/icons-material/VerifiedOutlined';
import { boxRadius } from '../../theme';
import { EChart } from '../EChart';
import { ResultTable } from '../ResultTable';
import { vizDisplay, reportChartOption, numericColumnIndices } from '../reportVisual';
import { DurumRayi } from './DurumRayi';
import { generateQuerySql } from '../../api/queries';
import type { AskQuestionResult, DurumCipi } from '../../api/soru';
import type { VisualizationType } from '../../api/reports';

const TUR_SIRASI: VisualizationType[] = ['Tablo', 'Cubuk', 'Cizgi', 'Pasta', 'SayiKarti'];

/** S31 — SONUÇ TUVALİ: cevabın TAM BOY yaşadığı yer.
 *
 * Kritik kural (tasarım araştırması): grafik ve tablo ASLA dar sohbet sütununda çizilmez. Sohbet
 * sütunu 65-75 karakterlik okuma genişliğinde kalır; çok boyutlu veri o kanala sıkışınca hem
 * okunmaz hem de kullanıcı "önceki cevap yukarı kayıp gitti" sorununu yaşar. Tuval tek ve
 * kalıcıdır: hangi mesaj seçiliyse onun sonucu burada durur. */
export function SonucTuvali({ cevap, sorulan, tur, onTur, onKaydet, onKanitAc, onCipKaldir, kaydedilebilir }: {
  cevap: AskQuestionResult;
  sorulan: string;
  tur: VisualizationType;
  onTur: (t: VisualizationType) => void;
  onKaydet: () => void;
  onKanitAc: (sekme: 'istem' | 'model' | 'sql') => void;
  onCipKaldir: (cip: DurumCipi) => void;
  kaydedilebilir: boolean;
}) {
  const [kanitAcik, setKanitAcik] = useState(false);
  const [kanitSekme, setKanitSekme] = useState(0);

  /* ÜRETİLEN SQL (kullanıcı bulgusu 24 Ağu 2026: "üretilen sql diyor ama bu json"). Modelin
   * kendisi JSON'dur; veritabanına giden SQL ondan ÜRETİLİR. Kanıt şeridinin asıl sorusu
   * "bana ne çalıştırıldı" olduğuna göre burada SQL durmalı — model JSON'u ayrı sekmede kalır.
   * Tembel çekilir: şerit açılıp SQL sekmesine gelinmeden istek atılmaz. */
  const [sql, setSql] = useState<string | null>(null);
  const [sqlHata, setSqlHata] = useState<string | null>(null);
  const sqlIstendi = useRef<AskQuestionResult | null>(null);

  useEffect(() => { setSql(null); setSqlHata(null); sqlIstendi.current = null; }, [cevap]);

  useEffect(() => {
    if (!kanitAcik || kanitSekme !== 1 || sqlIstendi.current === cevap) return;
    sqlIstendi.current = cevap;
    generateQuerySql(cevap.model)
      .then((u) => setSql(u.sql))
      .catch((e) => setSqlHata(e instanceof Error ? e.message : 'SQL üretilemedi.'));
  }, [kanitAcik, kanitSekme, cevap]);

  const grafik = useMemo(() => reportChartOption(cevap.sonuc, tur, cevap.model), [cevap, tur]);
  const sayisalIdx = useMemo(() => numericColumnIndices(cevap.sonuc), [cevap]);
  const kartOlcuIdx = useMemo(() => {
    const olcular: number[] = [];
    cevap.model.alanlar.forEach((a, i) => { if (a.toplama !== 'Yok') olcular.push(i); });
    return olcular.find((i) => sayisalIdx.includes(i)) ?? sayisalIdx[0] ?? -1;
  }, [cevap, sayisalIdx]);

  const onerilen = cevap.oneriGorsellestirme;

  return (
    <Paper variant="outlined" data-testid="nlq-cevap"
      sx={{ borderRadius: boxRadius, display: 'flex', flexDirection: 'column', overflow: 'hidden', minHeight: 0 }}>
      {/* başlık: ne sorulmuştu + aksiyonlar (kaydet SABİT burada — sohbet akışında değil) */}
      <Box sx={{ px: 1.75, py: 1.25, borderBottom: '1px solid', borderColor: 'divider', display: 'flex', gap: 1.25, alignItems: 'flex-start', flexWrap: 'wrap' }}>
        <Box sx={{ flex: 1, minWidth: 180 }}>
          <Typography sx={{ fontWeight: 800, fontSize: 14.5 }}>"{sorulan}"</Typography>
          <Typography sx={{ fontSize: 11.5, opacity: 0.75, mt: 0.2 }}>
            çeviri {(cevap.ceviriSureMs / 1000).toFixed(1)} sn · sorgu {cevap.sonuc.sureMs} ms · {cevap.sonuc.satirlar.length} satır
          </Typography>
        </Box>
        <Box sx={{ display: 'flex', gap: 0.5, alignItems: 'center' }}>
          {TUR_SIRASI.map((t) => {
            const g = vizDisplay[t];
            const secili = tur === t;
            const ai = onerilen === t;
            return (
              <Tooltip key={t} title={ai ? `${g.ad} — AI önerisi` : g.ad}>
                <Box onClick={() => onTur(t)} data-testid={`tuval-tur-${t}`}
                  sx={{
                    display: 'flex', alignItems: 'center', justifyContent: 'center', width: 30, height: 30,
                    borderRadius: '7px', cursor: 'pointer', border: '1px solid',
                    borderColor: secili ? 'secondary.main' : ai ? 'rgba(20,184,170,0.5)' : 'divider',
                    bgcolor: secili ? 'var(--kds-tealdolgu)' : 'var(--kds-zemin2)',
                    color: secili ? g.renk : 'text.secondary',
                    '&:hover': { borderColor: 'secondary.main' },
                  }}>
                  {g.ikon}
                </Box>
              </Tooltip>
            );
          })}
          {kaydedilebilir && (
            <Button size="small" variant="contained" startIcon={<SaveOutlinedIcon />} onClick={onKaydet}
              sx={{ ml: 0.5, whiteSpace: 'nowrap' }}>
              Rapor olarak kaydet
            </Button>
          )}
        </Box>
      </Box>

      {cevap.durum && <DurumRayi durum={cevap.durum} onKaldir={onCipKaldir} />}

      {/* görselleştirme */}
      <Box sx={{ p: 1.75, flex: 1, minHeight: 0, overflowY: 'auto', bgcolor: 'background.paper' }}>
        {tur === 'Tablo' ? (
          <ResultTable sonuc={cevap.sonuc} />
        ) : tur === 'SayiKarti' ? (
          kartOlcuIdx >= 0 ? <SayiKarti sonuc={cevap.sonuc} olcuIdx={kartOlcuIdx} /> : <GorunumOlmadi />
        ) : grafik ? (
          <EChart secenek={grafik} />
        ) : (
          <GorunumOlmadi />
        )}

        {/* ATIF SATIRI — daima açık: cevap neye dayanıyor, ne zaman doğrulanmış */}
        <Box sx={{
          mt: 1.5, px: 1.5, py: 1, border: '1px solid', borderColor: 'divider', borderRadius: boxRadius,
          bgcolor: 'var(--kds-zemin2)', display: 'flex', alignItems: 'center', gap: 1, flexWrap: 'wrap', fontSize: 12,
        }}>
          <VerifiedOutlinedIcon sx={{ fontSize: 15, color: 'success.main' }} />
          {cevap.durum && cevap.durum.tablolar.length > 0 && (
            <Typography sx={{ fontSize: 12 }}>
              <b>{cevap.durum.tablolar.join(', ')}</b> {cevap.durum.tablolar.length > 1 ? 'tabloları' : 'tablosu'}
            </Typography>
          )}
          {cevap.durum?.metrikler.map((m) => (
            <Typography key={m.id} sx={{ fontSize: 12 }}>
              · <b style={{ color: 'var(--kds-mor)' }}>{m.ad}</b> metriği
              {m.sonDegisiklik && (
                <Typography component="span" sx={{ fontSize: 11.5, opacity: 0.7 }}>
                  {' '}(tanım {new Date(m.sonDegisiklik).toLocaleDateString('tr-TR')})
                </Typography>
              )}
            </Typography>
          ))}
          <Typography sx={{ fontSize: 12 }}>· {cevap.sonuc.satirlar.length} satır</Typography>
          <Typography sx={{ fontSize: 12, color: 'success.main' }}>· satır kuralları (RLS) sizin yetkinizle uygulandı</Typography>
        </Box>

        {/* KANIT ŞERİDİ — üç ayrı düğme yerine tek katlanır alan */}
        <Paper variant="outlined" sx={{ mt: 1, borderRadius: boxRadius, overflow: 'hidden' }}>
          <Box onClick={() => setKanitAcik((a) => !a)} data-testid="kanit-serit"
            sx={{ px: 1.5, py: 1, display: 'flex', alignItems: 'center', gap: 1, cursor: 'pointer' }}>
            <ExpandMoreIcon sx={{ fontSize: 18, transition: 'transform .2s', transform: kanitAcik ? 'rotate(0)' : 'rotate(-90deg)' }} />
            <Typography sx={{ fontWeight: 700, fontSize: 12.5 }}>Kanıt</Typography>
            <Typography sx={{ fontSize: 12, opacity: 0.7 }}>Nasıl hesaplandı · Çalışan SQL · Sorgu modeli · Modele giden istem</Typography>
            <Typography sx={{ fontSize: 11.5, opacity: 0.6, ml: 'auto' }}>model yerelde çalıştı</Typography>
          </Box>
          <Collapse in={kanitAcik}>
            <Box sx={{ borderTop: '1px solid', borderColor: 'divider' }}>
              <Tabs value={kanitSekme} onChange={(_, v: number) => setKanitSekme(v)} variant="scrollable"
                sx={{ minHeight: 36, '& .MuiTab-root': { minHeight: 36, fontSize: 12.5, textTransform: 'none' } }}>
                <Tab label="Nasıl hesaplandı" />
                <Tab label="Çalışan SQL" />
                <Tab label="Sorgu modeli" />
                <Tab label="Modele giden istem" />
              </Tabs>
              <Box sx={{ p: 1.5, bgcolor: 'var(--kds-zemin2)' }}>
                {kanitSekme === 0 && (
                  <>
                    {cevap.nasilHesaplandi.map((m, i) => (
                      <Typography key={i} sx={{ fontSize: 12.5, opacity: 0.85 }}>• {m}</Typography>
                    ))}
                  </>
                )}
                {kanitSekme === 1 && (
                  <>
                    <Typography sx={{ fontSize: 12, opacity: 0.75, mb: 0.75 }}>
                      Veritabanında gerçekten çalışan sorgu. Parametreli üretilir; SELECT bekçisi, satır
                      güvenliği (RLS), satır sınırı ve denetim aynı hattan geçer.
                    </Typography>
                    {sqlHata ? (
                      <Typography sx={{ fontSize: 12.5, color: 'error.main' }}>{sqlHata}</Typography>
                    ) : sql === null ? (
                      <Typography sx={{ fontSize: 12.5, opacity: 0.7 }}>SQL üretiliyor…</Typography>
                    ) : (
                      <>
                        <Box component="pre" data-testid="nlq-sql"
                          sx={{ fontSize: 11.5, whiteSpace: 'pre-wrap', m: 0, maxHeight: 220, overflowY: 'auto',
                            fontFamily: 'ui-monospace, SFMono-Regular, Menlo, monospace' }}>
                          {sql}
                        </Box>
                        <Box sx={{ display: 'flex', gap: 0.5, mt: 0.5 }}>
                          <Button size="small" onClick={() => onKanitAc('sql')}>Büyük göster</Button>
                          <Button size="small" onClick={() => void navigator.clipboard?.writeText(sql)}>Kopyala</Button>
                        </Box>
                      </>
                    )}
                  </>
                )}
                {kanitSekme === 2 && (
                  <>
                    <Typography sx={{ fontSize: 12, opacity: 0.75, mb: 0.75 }}>
                      Model SQL üretmez — elle kurulmuş sorgudan farksız bu JSON modeli üretir; SQL yukarıdaki
                      sekmede, bu modelden üretilir. "Rapor olarak kaydet" de bunu taşır.
                    </Typography>
                    <Box component="pre" sx={{ fontSize: 11.5, whiteSpace: 'pre-wrap', m: 0, maxHeight: 220, overflowY: 'auto' }}>
                      {JSON.stringify(cevap.model, null, 2)}
                    </Box>
                    <Button size="small" sx={{ mt: 0.5 }} onClick={() => onKanitAc('model')}>Büyük göster</Button>
                  </>
                )}
                {kanitSekme === 3 && (
                  <>
                    <Typography sx={{ fontSize: 12, opacity: 0.75, mb: 0.75 }}>
                      Modele giden metnin TAMAMI budur — gölge kopya yok. Yalnız tablo/kolon adları ve tipleri +
                      metrik sözlüğü gider; satır verisi, bağlantı bilgisi, sunucu adı asla gitmez.
                    </Typography>
                    <Box component="pre" sx={{ fontSize: 11.5, whiteSpace: 'pre-wrap', m: 0, maxHeight: 220, overflowY: 'auto' }}>
                      {cevap.gidenIstem}
                    </Box>
                    <Button size="small" sx={{ mt: 0.5 }} onClick={() => onKanitAc('istem')}>Büyük göster</Button>
                  </>
                )}
              </Box>
            </Box>
          </Collapse>
        </Paper>
      </Box>
    </Paper>
  );
}

function SayiKarti({ sonuc, olcuIdx }: { sonuc: AskQuestionResult['sonuc']; olcuIdx: number }) {
  const deger = sonuc.satirlar[0]?.[olcuIdx];
  const sayi = typeof deger === 'number' ? deger.toLocaleString('tr-TR') : String(deger ?? '—');
  return (
    <Box sx={{ py: 5, textAlign: 'center' }}>
      <Typography sx={{ fontSize: 48, fontWeight: 800, letterSpacing: '-1px', color: 'secondary.main', lineHeight: 1 }}>{sayi}</Typography>
      <Typography sx={{ fontSize: 13, mt: 1 }} color="text.secondary">{sonuc.kolonlar[olcuIdx]?.ad ?? ''}</Typography>
      {sonuc.satirlar.length > 1 && (
        <Typography sx={{ fontSize: 11 }} color="text.disabled">ilk satırın değeri gösteriliyor ({sonuc.satirlar.length} satır)</Typography>
      )}
    </Box>
  );
}

function GorunumOlmadi() {
  return (
    <Box sx={{ p: 3, textAlign: 'center' }}>
      <Typography sx={{ fontSize: 13 }} color="text.secondary">
        Bu görünüm için <b>sayısal bir ölçü</b> gerekli — bu cevabın kolonları metin/kimlik.
        <b> Tablo</b> görünümünü kullanın.
      </Typography>
    </Box>
  );
}
