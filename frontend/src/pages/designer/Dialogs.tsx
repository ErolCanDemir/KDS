import Box from '@mui/material/Box';
import Dialog from '@mui/material/Dialog';
import DialogContent from '@mui/material/DialogContent';
import Typography from '@mui/material/Typography';
import Button from '@mui/material/Button';
import IconButton from '@mui/material/IconButton';
import TextField from '@mui/material/TextField';
import Select from '@mui/material/Select';
import MenuItem from '@mui/material/MenuItem';
import Radio from '@mui/material/Radio';
import RadioGroup from '@mui/material/RadioGroup';
import FormControlLabel from '@mui/material/FormControlLabel';
import AddIcon from '@mui/icons-material/Add';
import CloseIcon from '@mui/icons-material/Close';
import DeleteOutlineIcon from '@mui/icons-material/DeleteOutlined';
import { boxRadius, smallRadius } from '../../theme';
import type { DataMapDetail, DataMapRelationshipDto } from '../../api/dataMaps';
import type { AggregationType, ComparisonOp, LogicConnector, PencereIslevi } from '../../api/queries';
import {
  pairKey, isValueless, opLabel, opOrder, aggLabel, uuid, DialogLabel, IfadeGirisi,
  pencereIslevleri, pencereTanim,
  type EditComposite, type EditCompositePart, type EditFilter, type EditConditional,
  type EditCaseAlan, type EditCaseDal, type EditHesap, type EditSummary, type EditSummaryFilter,
  type EditPencere,
} from './shared';

// ---- Sorgu Tasarımcısı diyalogları (Y4 refactor: bölüm 3) ----
// Durum sahibi ANA SAYFADIR: buradaki bileşenler yalnız taslağı (taslak) çizer, değişikliği
// `change` ile bildirir; apply/close kararları da ana sayfanın verdiği callback'lerdir.
// Form geçerlilik kuralları (gecerli) formun yanında, bu dosyada yaşar.

/** Kanvasta kenara tıklanınca: bu tablo çifti hangi kolonlardan birleşsin? */
export type JoinTaslak = { cift: string; tidA: string; tidB: string; kolA: string; kolB: string };

export function JoinColumnDialog({ taslak: t, detay, oneriler, seciliMi, tableName, change, close, apply, otomatige }: {
  taslak: JoinTaslak;
  detay: DataMapDetail | null;
  /** Haritada bu çift için tanımlı ilişkiler (öneri çipleri). */
  oneriler: DataMapRelationshipDto[];
  /** Bu çift için daha önce elle seçim yapılmış mı ("Otomatiğe dön" görünür). */
  seciliMi: boolean;
  tableName: (id: string) => string;
  change: (d: Partial<JoinTaslak>) => void;
  close: () => void;
  apply: () => void;
  otomatige: () => void;
}) {
  const kolonlar = (tid: string) => detay?.tablolar.find((x) => x.id === tid)?.kolonlar ?? [];
  return (
    <Dialog open onClose={close} maxWidth="sm" fullWidth
      slotProps={{ paper: { sx: { borderRadius: boxRadius } } }}>
      <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', px: 3, pt: 2 }}>
        <Typography variant="h6" color="primary">Join kolonları</Typography>
        <IconButton size="small" onClick={close}><CloseIcon fontSize="small" /></IconButton>
      </Box>
      <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 1.75 }}>
        <Typography sx={{ fontSize: 12.5 }} color="text.secondary">
          <b>{tableName(t.tidA)}</b> ve <b>{tableName(t.tidB)}</b> tabloları hangi
          kolonlardan birleşsin? Otomatik seçilen alan yanlışsa buradan düzeltebilirsin.
        </Typography>

        {oneriler.length > 0 && (
          <Box>
            <DialogLabel>Haritadaki ilişkiler (öneri)</DialogLabel>
            <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 0.6 }}>
              {oneriler.map((i) => {
                const aKol = i.kaynakTabloId === t.tidA ? i.kaynakKolon : i.hedefKolon;
                const bKol = i.kaynakTabloId === t.tidA ? i.hedefKolon : i.kaynakKolon;
                const aktif = aKol === t.kolA && bKol === t.kolB;
                return (
                  <Box component="button" type="button" key={`${i.kaynakKolon}>${i.hedefKolon}`}
                    onClick={() => change({ kolA: aKol, kolB: bKol })}
                    sx={{ cursor: 'pointer', border: '1px solid', borderColor: aktif ? 'secondary.main' : '#d5dde6',
                      bgcolor: aktif ? 'rgba(14,111,122,.08)' : '#fff', color: 'text.primary',
                      borderRadius: smallRadius, px: 1, py: 0.4, fontSize: 11.5, display: 'inline-flex', alignItems: 'center', gap: 0.6,
                      fontFamily: 'inherit', '&:hover': { borderColor: 'secondary.main' } }}>
                    {aKol} = {bKol}
                    {i.fktenMi && <Box component="span" sx={{ fontSize: 9, color: 'success.main', fontWeight: 800 }}>FK</Box>}
                  </Box>
                );
              })}
            </Box>
          </Box>
        )}

        <Box sx={{ display: 'flex', alignItems: 'flex-end', gap: 1 }}>
          <Box sx={{ flex: 1 }}>
            <DialogLabel>{tableName(t.tidA)} kolonu</DialogLabel>
            <Select size="small" fullWidth value={t.kolA}
              onChange={(e) => change({ kolA: e.target.value })}>
              {kolonlar(t.tidA).map((k) => (
                <MenuItem key={k.hamAd} value={k.hamAd} sx={{ fontSize: 12.5 }}>
                  {k.hamAd}{k.veriTipi ? <Box component="span" sx={{ ml: 0.75, fontSize: 10, color: 'text.disabled' }}>{k.veriTipi}</Box> : null}
                </MenuItem>
              ))}
            </Select>
          </Box>
          <Typography sx={{ fontSize: 16, fontWeight: 700, color: 'secondary.main', pb: 0.6 }}>=</Typography>
          <Box sx={{ flex: 1 }}>
            <DialogLabel>{tableName(t.tidB)} kolonu</DialogLabel>
            <Select size="small" fullWidth value={t.kolB}
              onChange={(e) => change({ kolB: e.target.value })}>
              {kolonlar(t.tidB).map((k) => (
                <MenuItem key={k.hamAd} value={k.hamAd} sx={{ fontSize: 12.5 }}>
                  {k.hamAd}{k.veriTipi ? <Box component="span" sx={{ ml: 0.75, fontSize: 10, color: 'text.disabled' }}>{k.veriTipi}</Box> : null}
                </MenuItem>
              ))}
            </Select>
          </Box>
        </Box>

        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, mt: 0.5 }}>
          {seciliMi && (
            <Button size="small" color="inherit" onClick={otomatige}>Otomatiğe dön</Button>
          )}
          <Box sx={{ flexGrow: 1 }} />
          <Button size="small" color="inherit" onClick={close}>İptal</Button>
          <Button size="small" variant="contained" onClick={apply}
            disabled={!t.kolA || !t.kolB}>Uygula</Button>
        </Box>
      </DialogContent>
    </Dialog>
  );
}

/** Bağlı Tablo Özeti (alt sorgu): farklı tablodan, kendi filtresiyle özet kolon. */
export function SummaryDialog({ taslak: oz, detay, kullanilan, tableName, change, close, apply }: {
  taslak: EditSummary;
  detay: DataMapDetail | null;
  /** Kanvasta kullanılan tablo id'leri (bağlanılacak tablo adayları buradan süzülür). */
  kullanilan: string[];
  tableName: (id: string) => string;
  change: (d: Partial<EditSummary>) => void;
  close: () => void;
  apply: () => void;
}) {
  const ozetKolonlar = detay?.tablolar.find((t) => t.id === oz.tabloId)?.kolonlar ?? [];
  const bagAday = (tabloId: string) => kullanilan.filter((tid) => tid !== tabloId &&
    (detay?.iliskiler ?? []).some((i) => pairKey(i.kaynakTabloId, i.hedefTabloId) === pairKey(tabloId, tid)));
  const adaylar = oz.tabloId ? bagAday(oz.tabloId) : [];
  const gecerli = !!oz.tabloId && !!oz.kolon && adaylar.includes(oz.bagTabloId);
  const g = change;
  const fG = (anahtar: string, d: Partial<EditSummaryFilter>) =>
    g({ filtreler: oz.filtreler.map((f) => (f.anahtar === anahtar ? { ...f, ...d } : f)) });
  return (
    <Dialog open onClose={close} maxWidth="sm" fullWidth
      slotProps={{ paper: { sx: { borderRadius: boxRadius } } }}>
      <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', px: 3, pt: 2 }}>
        <Typography variant="h6" color="primary">Bağlı Tablo Özeti (alt sorgu)</Typography>
        <IconButton size="small" onClick={close}><CloseIcon fontSize="small" /></IconButton>
      </Box>
      <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 1.75 }}>
        {/* M6: bu alanın NE olduğu örnekle içeride anlatılır — SELECT'e kolon ekler, WHERE değildir. */}
        <Box sx={{ bgcolor: 'var(--kds-tealdolgu)', border: '1px solid #bfe0e5', borderRadius: smallRadius, px: 1.5, py: 1 }}>
          <Typography sx={{ fontSize: 12.5, fontWeight: 700, mb: 0.4 }} color="secondary.main">
            Bu alan SELECT'e alt sorgu eklemenizi sağlar — sonuca özet KOLON gelir; WHERE koşulu değildir
          </Typography>
          <Typography sx={{ fontSize: 12 }} color="text.secondary">
            Ana sorgunun satırlarını filtrelemez ve çoğaltmaz; her satırın yanına, ilişkili başka
            tablodan hesaplanan bir özet yazar (arka planda kendi filtreli bir alt sorgu LEFT JOIN'lenir;
            eşleşme yoksa Say/Topla <b>0</b> görünür). Satır ELEMEK istiyorsan onu Filtreler ya da
            Elle SQL koşulu (WHERE) yapar.
          </Typography>
          <Typography sx={{ fontSize: 12, mt: 0.6 }} color="text.secondary">
            <b>Örnek:</b> Talepler listelenirken her talebin yanında o talebin parça kalemi sayısı:
            Özetlenecek tablo <b>TalepParcalari</b> · Özet <b>Say (COUNT)</b> · Kolon <b>Id</b> ·
            Bağlanacağı tablo <b>Talepler</b> → her Talep satırında ör. <b>KalemSayisi = 3</b>.
            Özete özel filtreyle daraltılabilir (ör. yalnız <i>Adet &gt; 1</i> olan kalemleri say).
          </Typography>
        </Box>
        <Box sx={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 1.5 }}>
          <Box>
            <DialogLabel>Özetlenecek tablo</DialogLabel>
            <Select size="small" fullWidth displayEmpty value={oz.tabloId}
              onChange={(e) => { const v = e.target.value; g({ tabloId: v, kolon: '', filtreler: [], bagTabloId: bagAday(v)[0] ?? '' }); }}>
              <MenuItem value="" disabled><em>Tablo seç…</em></MenuItem>
              {detay?.tablolar.map((t) => <MenuItem key={t.id} value={t.id} sx={{ fontSize: 12.5 }}>{t.dostcaAd || t.hamAd}</MenuItem>)}
            </Select>
          </Box>
          <Box>
            <DialogLabel>Bağlanacağı tablo</DialogLabel>
            <Select size="small" fullWidth displayEmpty value={adaylar.includes(oz.bagTabloId) ? oz.bagTabloId : ''}
              onChange={(e) => g({ bagTabloId: e.target.value })} disabled={!oz.tabloId}>
              <MenuItem value="" disabled><em>{oz.tabloId ? (adaylar.length === 0 ? 'İlişkili tablo yok' : 'Tablo seç…') : 'Önce özet tablosu'}</em></MenuItem>
              {adaylar.map((tid) => <MenuItem key={tid} value={tid} sx={{ fontSize: 12.5 }}>{tableName(tid)}</MenuItem>)}
            </Select>
            {oz.tabloId && adaylar.length === 0 && (
              <Typography sx={{ fontSize: 10.5, mt: 0.4 }} color="error.main">
                Bu tablonun, kanvasta kullanılan tablolarla haritada ilişkisi yok.
              </Typography>
            )}
          </Box>
          <Box>
            <DialogLabel>Özet (toplama)</DialogLabel>
            <Select size="small" fullWidth value={oz.toplama} onChange={(e) => g({ toplama: e.target.value as AggregationType })}>
              {(['Say', 'BenzersizSay', 'Topla', 'Ortalama', 'EnAz', 'EnCok'] as AggregationType[])
                .map((t) => <MenuItem key={t} value={t} sx={{ fontSize: 12.5 }}>{aggLabel[t]}</MenuItem>)}
            </Select>
          </Box>
          <Box>
            <DialogLabel>Kolon</DialogLabel>
            <Select size="small" fullWidth displayEmpty value={oz.kolon}
              onChange={(e) => g({ kolon: e.target.value })} disabled={!oz.tabloId}>
              <MenuItem value="" disabled><em>Kolon seç…</em></MenuItem>
              {ozetKolonlar.map((k) => <MenuItem key={k.hamAd} value={k.hamAd} sx={{ fontSize: 12.5 }}>{k.dostcaAd || k.hamAd}</MenuItem>)}
            </Select>
          </Box>
        </Box>
        <Box>
          <DialogLabel>Görünecek ad (isteğe bağlı)</DialogLabel>
          <TextField size="small" fullWidth placeholder="ör. AktifCalisan" value={oz.takmaAd}
            onChange={(e) => g({ takmaAd: e.target.value })} />
        </Box>
        <Box>
          <DialogLabel>Özete özel filtre (yalnız {oz.tabloId ? tableName(oz.tabloId) : 'özet tablosu'} kolonları · VE ile birleşir)</DialogLabel>
          <Box sx={{ display: 'flex', flexDirection: 'column', gap: 0.6 }}>
            {oz.filtreler.map((f) => (
              <Box key={f.anahtar} sx={{ display: 'flex', alignItems: 'center', gap: 0.8, flexWrap: 'wrap' }}>
                <Select size="small" displayEmpty value={f.kolon} onChange={(e) => fG(f.anahtar, { kolon: e.target.value })} sx={{ minWidth: 130, fontSize: 12.5 }}>
                  <MenuItem value="" disabled><em>Kolon…</em></MenuItem>
                  {ozetKolonlar.map((k) => <MenuItem key={k.hamAd} value={k.hamAd} sx={{ fontSize: 12.5 }}>{k.dostcaAd || k.hamAd}</MenuItem>)}
                </Select>
                <Select size="small" value={f.op} onChange={(e) => fG(f.anahtar, { op: e.target.value as ComparisonOp })} sx={{ minWidth: 110, fontSize: 12.5 }}>
                  {opOrder.map((o) => <MenuItem key={o} value={o} sx={{ fontSize: 12.5 }}>{opLabel[o]}</MenuItem>)}
                </Select>
                {!isValueless(f.op) && <TextField size="small" placeholder={f.op === 'Icinde' ? 'a, b, c' : 'değer'} value={f.d1} onChange={(e) => fG(f.anahtar, { d1: e.target.value })} sx={{ width: 130 }} />}
                {f.op === 'Arasinda' && <TextField size="small" placeholder="ve" value={f.d2} onChange={(e) => fG(f.anahtar, { d2: e.target.value })} sx={{ width: 110 }} />}
                <IconButton size="small" onClick={() => g({ filtreler: oz.filtreler.filter((x) => x.anahtar !== f.anahtar) })} sx={{ p: 0.4 }}><DeleteOutlineIcon sx={{ fontSize: 16 }} /></IconButton>
              </Box>
            ))}
            <Box>
              <Button size="small" startIcon={<AddIcon />} disabled={!oz.tabloId} sx={{ fontSize: 12 }}
                onClick={() => g({ filtreler: [...oz.filtreler, { anahtar: uuid(), kolon: '', op: 'Esit', d1: '', d2: '' }] })}>
                Koşul ekle
              </Button>
            </Box>
          </Box>
        </Box>
        {/* M6: seçimler tamamlanınca bu özetin sonuca ne ekleyeceği düz cümleyle gösterilir. */}
        {gecerli && (
          <Typography sx={{ fontSize: 12, fontWeight: 600 }} color="secondary.main">
            Bu özet: her <b>{tableName(oz.bagTabloId)}</b> satırının yanına
            {' '}"<b>{oz.takmaAd.trim() || `${aggLabel[oz.toplama]} ${oz.kolon}`}</b>" kolonu ekler
            {' '}— {aggLabel[oz.toplama]} · {tableName(oz.tabloId)}.{oz.kolon}
            {oz.filtreler.length > 0 ? ' (özel filtreli)' : ''}. Satır sayısı değişmez.
          </Typography>
        )}
        <Box sx={{ display: 'flex', justifyContent: 'flex-end', gap: 1 }}>
          <Button color="inherit" onClick={close}>İptal</Button>
          <Button variant="contained" onClick={apply}
            disabled={!gecerli || oz.filtreler.some((f) => !f.kolon)}>Uygula</Button>
        </Box>
      </DialogContent>
    </Dialog>
  );
}

/** Koşullu Ölçü: AGG(CASE WHEN koşullar THEN değer …) yapısal kurulum. */
export function ConditionalDialog({ taslak: ko, tumKolonlar, change, close, apply }: {
  taslak: EditConditional;
  /** Haritadaki tüm kolonlar ("Tablo · Kolon" etiketiyle). */
  tumKolonlar: { tabloId: string; kolon: string; etiket: string }[];
  change: (d: Partial<EditConditional>) => void;
  close: () => void;
  apply: () => void;
}) {
  const g = change;
  const kG = (anahtar: string, d: Partial<EditFilter>) =>
    g({ kosullar: ko.kosullar.map((k) => (k.anahtar === anahtar ? { ...k, ...d } : k)) });
  // O1: BenzersizSay sabit-1 ile anlamsız (DISTINCT 1 ≤ 1) — değer kolonu şart.
  const gecerli = ko.toplama !== 'Yok' && ko.kosullar.length > 0
    && ko.kosullar.every((k) => k.tabloId && k.kolon)
    && !(ko.toplama === 'BenzersizSay' && !ko.kolon);
  return (
    <Dialog open onClose={close} maxWidth="sm" fullWidth
      slotProps={{ paper: { sx: { borderRadius: boxRadius } } }}>
      <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', px: 3, pt: 2 }}>
        <Typography variant="h6" color="primary">Koşullu Ölçü</Typography>
        <IconButton size="small" onClick={close}><CloseIcon fontSize="small" /></IconButton>
      </Box>
      <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 1.75 }}>
        <Typography sx={{ fontSize: 12.5 }} color="text.secondary">
          Yalnız <b>koşulu sağlayan satırları</b> özetler — ör. <i>garanti kapsamındaki talep
          sayısı</i>. SQL karşılığı: <Box component="code" sx={{ bgcolor: 'var(--kds-dolgu)', px: 0.5, borderRadius: '4px', fontSize: 11.5 }}>SUM(CASE WHEN … THEN … END)</Box>
        </Typography>
        <Box sx={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 1.5 }}>
          <Box>
            <DialogLabel>Özet (toplama)</DialogLabel>
            <Select size="small" fullWidth value={ko.toplama} onChange={(e) => g({ toplama: e.target.value as AggregationType })}>
              {(['Say', 'BenzersizSay', 'Topla', 'Ortalama', 'EnAz', 'EnCok'] as AggregationType[])
                .map((t) => <MenuItem key={t} value={t} sx={{ fontSize: 12.5 }}>{aggLabel[t]}</MenuItem>)}
            </Select>
          </Box>
          <Box>
            <DialogLabel>Değer kolonu (boş = adet say)</DialogLabel>
            <Select size="small" fullWidth displayEmpty value={ko.kolon ? `${ko.tabloId}|${ko.kolon}` : ''}
              onChange={(e) => { const [ti, kol] = (e.target.value as string).split('|'); g({ tabloId: ti ?? '', kolon: kol ?? '' }); }}>
              <MenuItem value="" sx={{ fontSize: 12.5 }}><em>— sabit 1 (adet) —</em></MenuItem>
              {tumKolonlar.map((k) => <MenuItem key={`${k.tabloId}|${k.kolon}`} value={`${k.tabloId}|${k.kolon}`} sx={{ fontSize: 12.5 }}>{k.etiket}</MenuItem>)}
            </Select>
          </Box>
        </Box>
        <Box>
          <DialogLabel>Görünecek ad (isteğe bağlı)</DialogLabel>
          <TextField size="small" fullWidth placeholder="ör. GarantiliTalep" value={ko.takmaAd}
            onChange={(e) => g({ takmaAd: e.target.value })} />
        </Box>
        <Box>
          <DialogLabel>Koşullar (VE ile birleşir)</DialogLabel>
          <Box sx={{ display: 'flex', flexDirection: 'column', gap: 0.6 }}>
            {ko.kosullar.map((f) => (
              <Box key={f.anahtar} sx={{ display: 'flex', alignItems: 'center', gap: 0.8, flexWrap: 'wrap' }}>
                <Select size="small" displayEmpty value={f.tabloId ? `${f.tabloId}|${f.kolon}` : ''}
                  onChange={(e) => { const [ti, kol] = (e.target.value as string).split('|'); kG(f.anahtar, { tabloId: ti ?? '', kolon: kol ?? '' }); }}
                  sx={{ minWidth: 180, fontSize: 12.5 }}>
                  <MenuItem value="" disabled><em>Kolon…</em></MenuItem>
                  {tumKolonlar.map((k) => <MenuItem key={`${k.tabloId}|${k.kolon}`} value={`${k.tabloId}|${k.kolon}`} sx={{ fontSize: 12.5 }}>{k.etiket}</MenuItem>)}
                </Select>
                <Select size="small" value={f.op} onChange={(e) => kG(f.anahtar, { op: e.target.value as ComparisonOp })} sx={{ minWidth: 110, fontSize: 12.5 }}>
                  {opOrder.map((o) => <MenuItem key={o} value={o} sx={{ fontSize: 12.5 }}>{opLabel[o]}</MenuItem>)}
                </Select>
                {!isValueless(f.op) && <TextField size="small" placeholder={f.op === 'Icinde' ? 'a, b, c' : 'değer'} value={f.d1} onChange={(e) => kG(f.anahtar, { d1: e.target.value })} sx={{ width: 120 }} />}
                {f.op === 'Arasinda' && <TextField size="small" placeholder="ve" value={f.d2} onChange={(e) => kG(f.anahtar, { d2: e.target.value })} sx={{ width: 100 }} />}
                <IconButton size="small" onClick={() => g({ kosullar: ko.kosullar.filter((x) => x.anahtar !== f.anahtar) })} sx={{ p: 0.4 }}><DeleteOutlineIcon sx={{ fontSize: 16 }} /></IconButton>
              </Box>
            ))}
            <Box>
              <Button size="small" startIcon={<AddIcon />} sx={{ fontSize: 12 }}
                onClick={() => g({ kosullar: [...ko.kosullar, { anahtar: uuid(), tabloId: '', kolon: '', op: 'Esit', d1: '', d2: '' }] })}>
                Koşul ekle
              </Button>
            </Box>
          </Box>
        </Box>
        <Box sx={{ display: 'flex', justifyContent: 'flex-end', gap: 1 }}>
          <Button color="inherit" onClick={close}>İptal</Button>
          <Button variant="contained" onClick={apply} disabled={!gecerli}>Uygula</Button>
        </Box>
      </DialogContent>
    </Dialog>
  );
}

/** M29 — CASE Kolonu: satır düzeyinde CASE WHEN … THEN … [ELSE …] END üretir (özet YOK;
 * özetli hâli Koşullu Ölçü'dür). Dallar sırayla denenir; ilk sağlanan koşulun sonucu yazılır. */
export function CaseDialog({ taslak: ca, tumKolonlar, change, close, apply }: {
  taslak: EditCaseAlan;
  /** Haritadaki tüm kolonlar ("Tablo · Kolon" etiketiyle). */
  tumKolonlar: { tabloId: string; kolon: string; etiket: string }[];
  change: (d: Partial<EditCaseAlan>) => void;
  close: () => void;
  apply: () => void;
}) {
  const g = change;
  const dalG = (anahtar: string, d: Partial<EditCaseDal>) =>
    g({ dallar: ca.dallar.map((x) => (x.anahtar === anahtar ? { ...x, ...d } : x)) });
  const kosulG = (dalAnahtar: string, kAnahtar: string, d: Partial<EditFilter>) =>
    g({ dallar: ca.dallar.map((x) => (x.anahtar === dalAnahtar
      ? { ...x, kosullar: x.kosullar.map((k) => (k.anahtar === kAnahtar ? { ...k, ...d } : k)) } : x)) });
  const sonucGecerli = (d: EditCaseDal) => (d.sonucTur === 'sabit' ? d.sabit.length > 0 : !!(d.tabloId && d.kolon));
  const gecerli = ca.dallar.length > 0
    && ca.dallar.every((d) => d.kosullar.length > 0 && d.kosullar.every((k) => k.tabloId && k.kolon) && sonucGecerli(d))
    && (ca.varsayilanTur !== 'kolon' || !!(ca.varsayilanTabloId && ca.varsayilanKolon));
  const kolonSecici = (deger: string, sec: (tabloId: string, kolon: string) => void, genislik = 180) => (
    <Select size="small" displayEmpty value={deger}
      onChange={(e) => { const [ti, kol] = (e.target.value as string).split('|'); sec(ti ?? '', kol ?? ''); }}
      sx={{ minWidth: genislik, fontSize: 12.5 }}>
      <MenuItem value="" disabled><em>Kolon…</em></MenuItem>
      {tumKolonlar.map((k) => <MenuItem key={`${k.tabloId}|${k.kolon}`} value={`${k.tabloId}|${k.kolon}`} sx={{ fontSize: 12.5 }}>{k.etiket}</MenuItem>)}
    </Select>
  );
  return (
    <Dialog open onClose={close} maxWidth="sm" fullWidth
      slotProps={{ paper: { sx: { borderRadius: boxRadius } } }}>
      <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', px: 3, pt: 2 }}>
        <Typography variant="h6" color="primary">CASE Kolonu</Typography>
        <IconButton size="small" onClick={close}><CloseIcon fontSize="small" /></IconButton>
      </Box>
      <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 1.75 }}>
        <Typography sx={{ fontSize: 12.5 }} color="text.secondary">
          Her satır için <b>koşula göre değer</b> üretir — ör. <i>durum etiketi</i>. SQL karşılığı:{' '}
          <Box component="code" sx={{ bgcolor: 'var(--kds-dolgu)', px: 0.5, borderRadius: '4px', fontSize: 11.5 }}>CASE WHEN … THEN … ELSE … END</Box>
          {' '}(özet içermez; özetli hâli için Koşullu Ölçü).
        </Typography>
        <Box>
          <DialogLabel>Görünecek ad (isteğe bağlı)</DialogLabel>
          <TextField size="small" fullWidth placeholder="ör. Durum" value={ca.takmaAd}
            onChange={(e) => g({ takmaAd: e.target.value })} />
        </Box>
        {ca.dallar.map((d, di) => (
          <Box key={d.anahtar} sx={{ border: '1px solid', borderColor: 'divider', borderRadius: smallRadius, p: 1.25, display: 'flex', flexDirection: 'column', gap: 0.8 }}>
            <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
              <Typography sx={{ fontSize: 11.5, fontWeight: 700 }} color="text.secondary">{di + 1}. dal — KOŞULLAR (VE ile birleşir)</Typography>
              <IconButton size="small" onClick={() => g({ dallar: ca.dallar.filter((x) => x.anahtar !== d.anahtar) })} sx={{ p: 0.4 }} title="Dalı kaldır"><DeleteOutlineIcon sx={{ fontSize: 16 }} /></IconButton>
            </Box>
            {d.kosullar.map((f) => (
              <Box key={f.anahtar} sx={{ display: 'flex', alignItems: 'center', gap: 0.8, flexWrap: 'wrap' }}>
                {kolonSecici(f.tabloId ? `${f.tabloId}|${f.kolon}` : '', (ti, kol) => kosulG(d.anahtar, f.anahtar, { tabloId: ti, kolon: kol }))}
                <Select size="small" value={f.op} onChange={(e) => kosulG(d.anahtar, f.anahtar, { op: e.target.value as ComparisonOp })} sx={{ minWidth: 110, fontSize: 12.5 }}>
                  {opOrder.map((o) => <MenuItem key={o} value={o} sx={{ fontSize: 12.5 }}>{opLabel[o]}</MenuItem>)}
                </Select>
                {!isValueless(f.op) && <TextField size="small" placeholder={f.op === 'Icinde' ? 'a, b, c' : 'değer'} value={f.d1} onChange={(e) => kosulG(d.anahtar, f.anahtar, { d1: e.target.value })} sx={{ width: 120 }} />}
                {f.op === 'Arasinda' && <TextField size="small" placeholder="ve" value={f.d2} onChange={(e) => kosulG(d.anahtar, f.anahtar, { d2: e.target.value })} sx={{ width: 100 }} />}
                <IconButton size="small" onClick={() => dalG(d.anahtar, { kosullar: d.kosullar.filter((x) => x.anahtar !== f.anahtar) })} sx={{ p: 0.4 }}><DeleteOutlineIcon sx={{ fontSize: 16 }} /></IconButton>
              </Box>
            ))}
            <Box>
              <Button size="small" startIcon={<AddIcon />} sx={{ fontSize: 12 }}
                onClick={() => dalG(d.anahtar, { kosullar: [...d.kosullar, { anahtar: uuid(), tabloId: '', kolon: '', op: 'Esit', d1: '', d2: '' }] })}>
                Koşul ekle
              </Button>
            </Box>
            <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.8, flexWrap: 'wrap' }}>
              <Typography sx={{ fontSize: 11.5, fontWeight: 700 }} color="text.secondary">SONUÇ →</Typography>
              <Select size="small" value={d.sonucTur} onChange={(e) => dalG(d.anahtar, { sonucTur: e.target.value as 'sabit' | 'kolon' })} sx={{ minWidth: 110, fontSize: 12.5 }}>
                <MenuItem value="sabit" sx={{ fontSize: 12.5 }}>Sabit değer</MenuItem>
                <MenuItem value="kolon" sx={{ fontSize: 12.5 }}>Kolon</MenuItem>
              </Select>
              {d.sonucTur === 'sabit'
                ? <TextField size="small" placeholder="ör. Aktif" value={d.sabit} onChange={(e) => dalG(d.anahtar, { sabit: e.target.value })} sx={{ width: 150 }} />
                : kolonSecici(d.tabloId ? `${d.tabloId}|${d.kolon}` : '', (ti, kol) => dalG(d.anahtar, { tabloId: ti, kolon: kol }), 160)}
            </Box>
          </Box>
        ))}
        <Box>
          <Button size="small" startIcon={<AddIcon />} sx={{ fontSize: 12 }}
            onClick={() => g({ dallar: [...ca.dallar, { anahtar: uuid(), kosullar: [{ anahtar: uuid(), tabloId: '', kolon: '', op: 'Esit', d1: '', d2: '' }], sonucTur: 'sabit', tabloId: '', kolon: '', sabit: '' }] })}>
            Dal ekle (WHEN)
          </Button>
        </Box>
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.8, flexWrap: 'wrap' }}>
          <Typography sx={{ fontSize: 11.5, fontWeight: 700 }} color="text.secondary">HİÇBİRİ SAĞLANMAZSA (ELSE)</Typography>
          <Select size="small" value={ca.varsayilanTur} onChange={(e) => g({ varsayilanTur: e.target.value as EditCaseAlan['varsayilanTur'] })} sx={{ minWidth: 120, fontSize: 12.5 }}>
            <MenuItem value="yok" sx={{ fontSize: 12.5 }}>— boş (NULL) —</MenuItem>
            <MenuItem value="sabit" sx={{ fontSize: 12.5 }}>Sabit değer</MenuItem>
            <MenuItem value="kolon" sx={{ fontSize: 12.5 }}>Kolon</MenuItem>
          </Select>
          {ca.varsayilanTur === 'sabit' && <TextField size="small" placeholder="ör. Pasif" value={ca.varsayilanSabit} onChange={(e) => g({ varsayilanSabit: e.target.value })} sx={{ width: 150 }} />}
          {ca.varsayilanTur === 'kolon' && kolonSecici(ca.varsayilanTabloId ? `${ca.varsayilanTabloId}|${ca.varsayilanKolon}` : '', (ti, kol) => g({ varsayilanTabloId: ti, varsayilanKolon: kol }), 160)}
        </Box>
        <Box sx={{ display: 'flex', justifyContent: 'flex-end', gap: 1 }}>
          <Button color="inherit" onClick={close}>İptal</Button>
          <Button variant="contained" onClick={apply} disabled={!gecerli}>Uygula</Button>
        </Box>
      </DialogContent>
    </Dialog>
  );
}

/** Birleşik Kolon: kolonlar + sabit metin ayraçları tek çıktı kolonunda. */
export function CompositeDialog({ taslak: bl, detay, change, close, apply }: {
  taslak: EditComposite;
  detay: DataMapDetail | null;
  change: (d: Partial<EditComposite>) => void;
  close: () => void;
  apply: () => void;
}) {
  const g = change;
  const pG = (anahtar: string, d: Partial<EditCompositePart>) =>
    g({ parcalar: bl.parcalar.map((p) => (p.anahtar === anahtar ? { ...p, ...d } : p)) });
  const gecerli = bl.parcalar.length > 0 &&
    bl.parcalar.every((p) => p.tur === 'sabit' || (p.tabloId && p.kolon)) &&
    bl.parcalar.some((p) => p.tur === 'kolon');
  return (
    <Dialog open onClose={close} maxWidth="sm" fullWidth
      slotProps={{ paper: { sx: { borderRadius: boxRadius } } }}>
      <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', px: 3, pt: 2 }}>
        <Typography variant="h6" color="primary">Birleşik Kolon</Typography>
        <IconButton size="small" onClick={close}><CloseIcon fontSize="small" /></IconButton>
      </Box>
      <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 1.75 }}>
        <Typography sx={{ fontSize: 12.5 }} color="text.secondary">
          Birden çok kolonu (araya metin koyarak) <b>tek kolon</b> olarak sun — ör. Ad + boşluk +
          Soyad → <i>KisiAdSoyad</i>. Parçalar sırayla birleşir; boş değerler zinciri bozmaz.
        </Typography>
        <Box>
          <DialogLabel>Görünecek ad</DialogLabel>
          <TextField size="small" fullWidth placeholder="ör. KisiAdSoyad" value={bl.takmaAd}
            onChange={(e) => g({ takmaAd: e.target.value })} />
        </Box>
        <Box>
          <DialogLabel>Parçalar (sırayla birleşir)</DialogLabel>
          <Box sx={{ display: 'flex', flexDirection: 'column', gap: 0.6 }}>
            {bl.parcalar.map((p) => (
              <Box key={p.anahtar} sx={{ display: 'flex', alignItems: 'center', gap: 0.8, flexWrap: 'wrap' }}>
                <Select size="small" value={p.tur}
                  onChange={(e) => pG(p.anahtar, { tur: e.target.value as 'kolon' | 'sabit' })}
                  sx={{ minWidth: 92, fontSize: 12.5 }}>
                  <MenuItem value="kolon" sx={{ fontSize: 12.5 }}>Kolon</MenuItem>
                  <MenuItem value="sabit" sx={{ fontSize: 12.5 }}>Metin</MenuItem>
                </Select>
                {p.tur === 'kolon' ? (
                  <>
                    <Select size="small" displayEmpty value={p.tabloId}
                      onChange={(e) => pG(p.anahtar, { tabloId: e.target.value, kolon: '' })}
                      sx={{ minWidth: 130, fontSize: 12.5 }}>
                      <MenuItem value="" disabled><em>Tablo…</em></MenuItem>
                      {detay?.tablolar.map((t) => <MenuItem key={t.id} value={t.id} sx={{ fontSize: 12.5 }}>{t.dostcaAd || t.hamAd}</MenuItem>)}
                    </Select>
                    <Select size="small" displayEmpty value={p.kolon} disabled={!p.tabloId}
                      onChange={(e) => pG(p.anahtar, { kolon: e.target.value })}
                      sx={{ minWidth: 130, fontSize: 12.5 }}>
                      <MenuItem value="" disabled><em>Kolon…</em></MenuItem>
                      {(detay?.tablolar.find((t) => t.id === p.tabloId)?.kolonlar ?? [])
                        .map((k) => <MenuItem key={k.hamAd} value={k.hamAd} sx={{ fontSize: 12.5 }}>{k.dostcaAd || k.hamAd}</MenuItem>)}
                    </Select>
                  </>
                ) : (
                  <>
                    <TextField size="small" placeholder="ayraç metni" value={p.sabit}
                      onChange={(e) => pG(p.anahtar, { sabit: e.target.value })} sx={{ width: 150 }} />
                    {/* boşluk kutuda görünmez — ne olduğunu söyle (test dönüşü #5) */}
                    <Typography sx={{ fontSize: 10.5 }} color="text.disabled">
                      {p.sabit === ' ' ? '␣ boşluk' : p.sabit === '' ? 'boş' : `"${p.sabit}"`} — kolonların ARASINA girer
                    </Typography>
                  </>
                )}
                <IconButton size="small" onClick={() => g({ parcalar: bl.parcalar.filter((x) => x.anahtar !== p.anahtar) })} sx={{ p: 0.4 }}><DeleteOutlineIcon sx={{ fontSize: 16 }} /></IconButton>
              </Box>
            ))}
            <Box>
              <Button size="small" startIcon={<AddIcon />} sx={{ fontSize: 12 }}
                onClick={() => g({ parcalar: [...bl.parcalar, { anahtar: uuid(), tur: 'kolon', tabloId: '', kolon: '', sabit: '' }] })}>
                Parça ekle
              </Button>
            </Box>
          </Box>
        </Box>
        <Box sx={{ display: 'flex', justifyContent: 'flex-end', gap: 1 }}>
          <Button color="inherit" onClick={close}>İptal</Button>
          <Button variant="contained" onClick={apply} disabled={!gecerli}>Uygula</Button>
        </Box>
      </DialogContent>
    </Dialog>
  );
}

/** Elle SQL koşulu: WHERE'e serbest koşul (alt sorgu dahil), {Tablo.Kolon} yer tutucularıyla. */
export function RawConditionDialog({ taslak, baglac, kaldirilebilir, change, baglacDegistir, close, apply, kaldir }: {
  /** Diyalogdaki taslak metin (uygulanmış koşul değil). */
  taslak: string;
  baglac: LogicConnector;
  /** Uygulanmış bir koşul var mı ("Koşulu kaldır" görünür). */
  kaldirilebilir: boolean;
  change: (v: string) => void;
  baglacDegistir: (b: LogicConnector) => void;
  close: () => void;
  apply: () => void;
  kaldir: () => void;
}) {
  return (
    <Dialog open onClose={close} maxWidth="sm" fullWidth
      slotProps={{ paper: { sx: { borderRadius: boxRadius } } }}>
      <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', px: 3, pt: 2 }}>
        <Typography variant="h6" color="primary">Elle SQL Koşulu</Typography>
        <IconButton size="small" onClick={close}><CloseIcon fontSize="small" /></IconButton>
      </Box>
      <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 1.5 }}>
        <Typography sx={{ fontSize: 12.5 }} color="text.secondary">
          WHERE'e eklenecek serbest koşul — alt sorgu da yazabilirsin. Kolonlara{' '}
          <Box component="code" sx={{ bgcolor: 'var(--kds-dolgu)', px: 0.5, borderRadius: '4px', fontSize: 12 }}>{'{Tablo.Kolon}'}</Box>{' '}
          biçimiyle değin; doğru takma ada otomatik çevrilir. Diğer filtrelerle <b>VE</b> ile birleşir.
        </Typography>
        <TextField multiline minRows={4} fullWidth value={taslak}
          onChange={(e) => change(e.target.value)}
          placeholder={'ör. {Talepler.Id} IN (SELECT TalepId FROM talep.TalepParcalari WHERE Adet > 2)'}
          slotProps={{ input: { sx: { fontFamily: 'Consolas, monospace', fontSize: 12.5 } } }} />
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
          <Typography sx={{ fontSize: 12, fontWeight: 700 }} color="primary">Diğer filtrelerle:</Typography>
          <RadioGroup row value={baglac} onChange={(e) => baglacDegistir(e.target.value as LogicConnector)}>
            <FormControlLabel value="Ve" control={<Radio size="small" />} label={<Typography sx={{ fontSize: 12.5 }}>VE (ikisi de sağlansın)</Typography>} />
            <FormControlLabel value="Veya" control={<Radio size="small" />} label={<Typography sx={{ fontSize: 12.5 }}>VEYA (biri yetsin)</Typography>} />
          </RadioGroup>
        </Box>
        <Typography sx={{ fontSize: 11 }} color="text.disabled">
          Güvenlik: sorgu yine tek-SELECT bekçisinden geçer; ikinci ifade/DML reddedilir.
        </Typography>
        <Box sx={{ display: 'flex', gap: 1 }}>
          {kaldirilebilir && (
            <Button size="small" color="inherit" onClick={kaldir}>Koşulu kaldır</Button>
          )}
          <Box sx={{ flexGrow: 1 }} />
          <Button color="inherit" onClick={close}>İptal</Button>
          <Button variant="contained" onClick={apply}>Uygula</Button>
        </Box>
      </DialogContent>
    </Dialog>
  );
}

/** Y1 — Hesaplanan Kolon: SELECT'e serbest FORMÜL. Kolonlar {Tablo.Kolon} yer tutucusuyla yazılır
 * (elle SQL koşuluyla aynı dil); gerisi hesaptır. Toplama (SUM/COUNT…) içeren formül GRUP
 * düzeyindedir, içermeyen SATIR düzeyi bir boyuttur — bu ayrımı backend kendisi yapar. */
export function HesapDialog({ taslak: h, tumKolonlar, change, close, apply }: {
  taslak: EditHesap;
  tumKolonlar: { tabloId: string; kolon: string; etiket: string; yerTutucu: string }[];
  change: (d: Partial<EditHesap>) => void;
  close: () => void;
  apply: () => void;
}) {
  const ekle = (metin: string) => change({ ifade: (h.ifade + (h.ifade.endsWith(' ') || !h.ifade ? '' : ' ') + metin) });
  return (
    <Dialog open onClose={close} maxWidth="sm" fullWidth slotProps={{ paper: { sx: { borderRadius: boxRadius } } }}>
      <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', px: 3, pt: 2 }}>
        <Typography variant="h6" color="primary">Hesaplanan Kolon (formül)</Typography>
        <IconButton size="small" onClick={close}><CloseIcon fontSize="small" /></IconButton>
      </Box>
      <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 1.5 }}>
        <Typography sx={{ fontSize: 12.5 }} color="text.secondary">
          Kolonlar arasında <b>hesap</b> yapar ve sonucu yeni bir kolon olarak ekler — fark, oran,
          yüzde, yuvarlama… Kolon eklemek için aşağıdaki listeyi kullan (metne
          <Box component="code" sx={{ bgcolor: 'var(--kds-dolgu)', px: 0.5, borderRadius: '4px', fontSize: 11.5, mx: 0.5 }}>{'{Tablo.Kolon}'}</Box>
          olarak girer).
        </Typography>
        <Box>
          <DialogLabel>Görünecek ad (isteğe bağlı)</DialogLabel>
          <TextField size="small" fullWidth placeholder="ör. Fark" value={h.takmaAd}
            onChange={(e) => change({ takmaAd: e.target.value })} />
        </Box>
        <Box>
          <DialogLabel>Formül</DialogLabel>
          <TextField size="small" fullWidth multiline minRows={3}
            placeholder="{Odemeler.Tutar} - {Faturalar.GenelToplam}"
            value={h.ifade} onChange={(e) => change({ ifade: e.target.value })}
            slotProps={{ input: { sx: { fontFamily: 'ui-monospace, Consolas, monospace', fontSize: 12.5 } } }} />
        </Box>
        <Box sx={{ display: 'flex', gap: 1, flexWrap: 'wrap', alignItems: 'center' }}>
          <Select size="small" value="" displayEmpty sx={{ minWidth: 190, fontSize: 12 }}
            onChange={(e) => ekle(e.target.value as string)}
            renderValue={() => <Box component="span" sx={{ color: 'secondary.main', fontWeight: 600 }}>＋ kolon ekle</Box>}>
            {tumKolonlar.map((k) => <MenuItem key={k.yerTutucu} value={k.yerTutucu} sx={{ fontSize: 12.5 }}>{k.etiket}</MenuItem>)}
          </Select>
          {['+', '-', '*', '/', '( )'].map((o) => (
            <Button key={o} size="small" variant="outlined" sx={{ minWidth: 34, px: 0.5, fontSize: 13 }}
              onClick={() => ekle(o === '( )' ? '()' : o)}>{o}</Button>
          ))}
        </Box>
        <Box sx={{ bgcolor: 'var(--kds-zemin2)', border: '1px solid', borderColor: 'divider', borderRadius: smallRadius, p: 1.25 }}>
          <Typography sx={{ fontSize: 11.5, fontWeight: 700, mb: 0.4 }} color="text.secondary">HAZIR KALIPLAR</Typography>
          {[
            { ad: 'Fark', f: '{A.Kolon} - {B.Kolon}' },
            { ad: 'Yüzde (sıfıra bölünmeye karşı korumalı)', f: 'SUM({A.Kolon}) * 100.0 / NULLIF(SUM({B.Kolon}), 0)' },
            { ad: 'Boşsa 0 say', f: 'ISNULL({A.Kolon}, 0)' },
            { ad: '2 basamağa yuvarla', f: 'ROUND({A.Kolon}, 2)' },
            { ad: 'Mutlak değer', f: 'ABS({A.Kolon} - {B.Kolon})' },
            { ad: 'İki tarih arası gün', f: 'DATEDIFF(DAY, {A.Baslangic}, {A.Bitis})' },
          ].map((k) => (
            <Typography key={k.ad} sx={{ fontSize: 11.5, lineHeight: 1.7 }} color="text.secondary">
              • {k.ad}: <Box component="code" sx={{ fontSize: 11 }}>{k.f}</Box>
            </Typography>
          ))}
          <Typography sx={{ fontSize: 11, mt: 0.6 }} color="text.disabled">
            SUM/COUNT gibi bir toplama yazarsan kolon GRUP düzeyinde olur; yazmazsan satır düzeyinde
            kalır ve gruplamaya katılır. Yalnız hesap yazılabilir — sorgu (SELECT) yazılamaz.
          </Typography>
        </Box>
        <Box sx={{ display: 'flex', justifyContent: 'flex-end', gap: 1 }}>
          <Button color="inherit" onClick={close}>İptal</Button>
          <Button variant="contained" onClick={apply} disabled={!h.ifade.trim()}>Uygula</Button>
        </Box>
      </DialogContent>
    </Dialog>
  );
}

/** Y6 — pencere kolonu: satırı KOMŞULARINA göre değerlendiren kolon (sıra no, önceki dönem,
 * kümülatif, bölüm toplamı, hareketli ortalama). Değer/bölüm/sıra alanları aynı formül dilini
 * kullanır: listeden seçilen kolon `{Tablo.Kolon}`, sonuçtaki kolon `[Ad]` olarak metne girer. */
export function PencereDialog({ taslak: p, tumKolonlar, cikisKolonlari, change, close, apply }: {
  taslak: EditPencere;
  tumKolonlar: { tabloId: string; kolon: string; etiket: string; yerTutucu: string }[];
  cikisKolonlari: { ad: string; tur: string }[];
  change: (d: Partial<EditPencere>) => void;
  close: () => void;
  apply: () => void;
}) {
  const tanim = pencereTanim(p.islev);
  // İfade kutuları ORTAK bileşenle (shared.IfadeGirisi) çizilir; burada bir sarmalayıcı bileşen
  // TANIMLANMAZ — gövde içinde tanımlanan bileşen her render'da yeni bir tip olur, React alt ağacı
  // yeniden monte eder ve kutu ODAĞI KAYBEDER (her karakterde imleç düşer).
  const gecerli = (!tanim.degerli || p.deger.trim().length > 0)
    && (!tanim.siraZorunlu || p.sira.some((s) => s.ifade.trim()));
  return (
    <Dialog open onClose={close} maxWidth="sm" fullWidth slotProps={{ paper: { sx: { borderRadius: boxRadius } } }}>
      <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', px: 3, pt: 2 }}>
        <Typography variant="h6" color="primary">Pencere Kolonu (satırı komşularına göre hesapla)</Typography>
        <IconButton size="small" onClick={close}><CloseIcon fontSize="small" /></IconButton>
      </Box>
      <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 1.5 }}>
        <Typography sx={{ fontSize: 12.5 }} color="text.secondary">
          Normal kolonlar yalnız KENDİ satırına bakar. Buradaki kolon, aynı pencerede yer alan
          <b> komşu satırlara</b> bakarak hesaplanır: sıra numarası, geçen dönemin değeri, kümülatif
          toplam, bölümün toplamı… Satırları elemez, sonuca yeni bir kolon ekler.
        </Typography>
        <Box>
          <DialogLabel>Ne hesaplansın?</DialogLabel>
          <Select size="small" fullWidth value={p.islev}
            onChange={(e) => {
              const yeni = e.target.value as PencereIslevi;
              change({ islev: yeni, sayi: pencereTanim(yeni).sayiVarsayilan ?? '' });
            }}
            sx={{ fontSize: 12.5 }}>
            {pencereIslevleri.map((i) => <MenuItem key={i.deger} value={i.deger} sx={{ fontSize: 12.5 }}>{i.etiket}</MenuItem>)}
          </Select>
          <Typography sx={{ fontSize: 11.5, mt: 0.5 }} color="text.secondary">{tanim.aciklama}</Typography>
        </Box>
        {tanim.degerli && (
          <Box>
            <DialogLabel>Hangi değer</DialogLabel>
            <IfadeGirisi deger={p.deger} yaz={(v) => change({ deger: v })} ipucu="ör. [Ciro] ya da {Siparisler.Tutar}" tumKolonlar={tumKolonlar} cikisKolonlari={cikisKolonlari} />
          </Box>
        )}
        <Box>
          <DialogLabel>Her … içinde ayrı hesapla (isteğe bağlı) — ör. her [Marka] içinde</DialogLabel>
          <Box sx={{ display: 'flex', flexDirection: 'column', gap: 0.6 }}>
            {p.bolum.map((b, i) => (
              <Box key={i} sx={{ display: 'flex', gap: 0.5, alignItems: 'center' }}>
                <IfadeGirisi deger={b} ipucu="ör. [Marka]" tumKolonlar={tumKolonlar} cikisKolonlari={cikisKolonlari}
                  yaz={(v) => change({ bolum: p.bolum.map((x, xi) => (xi === i ? v : x)) })} />
                <IconButton size="small" onClick={() => change({ bolum: p.bolum.filter((_, xi) => xi !== i) })} sx={{ p: 0.3 }}><DeleteOutlineIcon sx={{ fontSize: 16 }} /></IconButton>
              </Box>
            ))}
            <Box>
              <Button size="small" startIcon={<AddIcon />} sx={{ fontSize: 12 }}
                onClick={() => change({ bolum: [...p.bolum, ''] })}>Bölüm ekle</Button>
              <Typography component="span" sx={{ fontSize: 11, ml: 1 }} color="text.disabled">
                boş bırakılırsa hesap TÜM satırlar üzerinde yapılır
              </Typography>
            </Box>
          </Box>
        </Box>
        {p.islev !== 'BolumToplami' && (
          <Box>
            <DialogLabel>Neye göre sıralı? {tanim.siraZorunlu && <Box component="span" sx={{ color: 'error.main' }}>(zorunlu)</Box>}</DialogLabel>
            <Box sx={{ display: 'flex', flexDirection: 'column', gap: 0.6 }}>
              {p.sira.map((s) => (
                <Box key={s.anahtar} sx={{ display: 'flex', gap: 0.5, alignItems: 'center' }}>
                  <IfadeGirisi deger={s.ifade} ipucu="ör. [Ay] ya da {Siparisler.Tarih}" tumKolonlar={tumKolonlar} cikisKolonlari={cikisKolonlari}
                    yaz={(v) => change({ sira: p.sira.map((x) => (x.anahtar === s.anahtar ? { ...x, ifade: v } : x)) })} />
                  <Select size="small" value={s.azalan ? 'desc' : 'asc'} sx={{ minWidth: 92, fontSize: 12 }}
                    onChange={(e) => change({ sira: p.sira.map((x) => (x.anahtar === s.anahtar ? { ...x, azalan: e.target.value === 'desc' } : x)) })}>
                    <MenuItem value="asc" sx={{ fontSize: 12.5 }}>Artan ↑</MenuItem>
                    <MenuItem value="desc" sx={{ fontSize: 12.5 }}>Azalan ↓</MenuItem>
                  </Select>
                  <IconButton size="small" onClick={() => change({ sira: p.sira.filter((x) => x.anahtar !== s.anahtar) })} sx={{ p: 0.3 }}><DeleteOutlineIcon sx={{ fontSize: 16 }} /></IconButton>
                </Box>
              ))}
              <Box>
                <Button size="small" startIcon={<AddIcon />} sx={{ fontSize: 12 }}
                  onClick={() => change({ sira: [...p.sira, { anahtar: uuid(), ifade: '', azalan: false }] })}>Sıra ekle</Button>
              </Box>
            </Box>
          </Box>
        )}
        {tanim.sayiEtiket && (
          <Box sx={{ width: 220 }}>
            <DialogLabel>{tanim.sayiEtiket}</DialogLabel>
            <TextField size="small" type="number" fullWidth value={p.sayi}
              onChange={(e) => change({ sayi: e.target.value })} />
          </Box>
        )}
        <Box>
          <DialogLabel>Görünecek ad (isteğe bağlı)</DialogLabel>
          <TextField size="small" fullWidth placeholder="ör. Geçen Ay Ciro" value={p.takmaAd}
            onChange={(e) => change({ takmaAd: e.target.value })} />
        </Box>
        <Box sx={{ bgcolor: 'var(--kds-zemin2)', border: '1px solid', borderColor: 'divider', borderRadius: smallRadius, p: 1.25 }}>
          <Typography sx={{ fontSize: 11.5, lineHeight: 1.7 }} color="text.secondary">
            • “Bölümün toplamı” sıra ALMAZ — sıra verilirse kümülatif toplama dönüşür ve “pay %”
            yanlış çıkardı; bu yüzden o işlevde sıra yok sayılır.<br />
            • Pencere kolonu gruplamadan SONRA hesaplanır: gruplanmış bir sorguda değer olarak bir
            özet kolona atıf yapın (<Box component="code" sx={{ fontSize: 11 }}>[Ciro]</Box>) ya da
            toplama yazın (<Box component="code" sx={{ fontSize: 11 }}>{'SUM({T.Tutar})'}</Box>).<br />
            • “Bölümün toplamı” + <b>Hesaplanan Kolon</b> birlikte “pay %” verir:
            <Box component="code" sx={{ fontSize: 11 }}>[Ciro] * 100.0 / NULLIF([GenelToplam], 0)</Box>
          </Typography>
        </Box>
        <Box sx={{ display: 'flex', justifyContent: 'flex-end', gap: 1 }}>
          <Button color="inherit" onClick={close}>İptal</Button>
          <Button variant="contained" onClick={apply} disabled={!gecerli}>Uygula</Button>
        </Box>
      </DialogContent>
    </Dialog>
  );
}
