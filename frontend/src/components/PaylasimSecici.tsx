import { useEffect, useState } from 'react';
import Box from '@mui/material/Box';
import Chip from '@mui/material/Chip';
import FormControlLabel from '@mui/material/FormControlLabel';
import Radio from '@mui/material/Radio';
import RadioGroup from '@mui/material/RadioGroup';
import Typography from '@mui/material/Typography';
import type { ReportSharing } from '../api/reports';
import { getRoller } from '../api/roller';
import { useSession } from '../identity/sessionContext';
import { Roles } from '../identity/roles';
import { kadiHata } from '../kadi/kadiKanal';

/** S27 — ortak paylaşım seçicisi (rapor kaydet + pano editörü): Özel · Tüm ekip · Belirli roller.
 * "Belirli roller"de rol çipleri çoklu seçilir; liste Roles & Yetkiler'deki gerçek rollerdir.
 * Rol listesi yüklenemezse (Keycloak yapılandırılmamış vb.) seçenek gizlenmez, uyarı gösterilir —
 * mevcut seçim kaybolmasın. admin sistem rolü çip olarak sunulmaz (admin zaten her şeyi görür). */
export function PaylasimSecici({ paylasim, roller, degisti }: {
  paylasim: ReportSharing;
  roller: string[];
  degisti: (paylasim: ReportSharing, roller: string[]) => void;
}) {
  const oturum = useSession();
  const [tumRoller, setTumRoller] = useState<string[] | null>(null);
  const [yuklemeHatasi, setYuklemeHatasi] = useState(false);

  useEffect(() => {
    if (paylasim !== 'Roller' || tumRoller !== null) return;
    // Rol listesi admin ucundan gelir; admin olmayan tasarımcı için mevcut seçim + kendi rolleri gösterilir.
    if (oturum.roller.includes(Roles.Admin)) {
      getRoller()
        .then((r) => setTumRoller(r.filter((x) => x.ad !== Roles.Admin).map((x) => x.ad)))
        .catch(() => { setYuklemeHatasi(true); kadiHata(); setTumRoller([]); });
    } else {
      setTumRoller([...new Set([...roller, ...oturum.roller.filter((r) => r !== Roles.Admin)])]);
    }
  }, [paylasim, tumRoller, roller, oturum.roller]);

  const sec = (rol: string) => {
    const yeni = roller.includes(rol) ? roller.filter((r) => r !== rol) : [...roller, rol];
    degisti('Roller', yeni);
  };

  const gorunecekler = [...new Set([...(tumRoller ?? []), ...roller])];

  return (
    <Box>
      <RadioGroup row value={paylasim}
        onChange={(e) => degisti(e.target.value as ReportSharing, roller)} sx={{ pt: 0.25 }}>
        <FormControlLabel value="Ozel" control={<Radio size="small" />}
          label={<Typography sx={{ fontSize: 12.5 }}>Özel</Typography>} />
        <FormControlLabel value="Ekip" control={<Radio size="small" />}
          label={<Typography sx={{ fontSize: 12.5 }}>Ekiple paylaş</Typography>} />
        <FormControlLabel value="Roller" control={<Radio size="small" />}
          label={<Typography sx={{ fontSize: 12.5 }}>Belirli roller</Typography>} />
      </RadioGroup>
      {paylasim === 'Roller' && (
        <Box sx={{ mt: 0.5 }}>
          <Box sx={{ display: 'flex', gap: 0.75, flexWrap: 'wrap' }}>
            {gorunecekler.map((rol) => (
              <Chip key={rol} label={rol} size="small" onClick={() => sec(rol)}
                color={roller.includes(rol) ? 'secondary' : 'default'}
                variant={roller.includes(rol) ? 'filled' : 'outlined'}
                sx={{ fontWeight: 700, fontSize: 11.5 }} />
            ))}
            {gorunecekler.length === 0 && (
              <Typography sx={{ fontSize: 12 }} color="text.secondary">
                {yuklemeHatasi ? 'Rol listesi alınamadı — Roles & Yetkiler ekranını denetleyin.' : 'Rol bulunamadı.'}
              </Typography>
            )}
          </Box>
          <Typography sx={{ fontSize: 11, mt: 0.5 }} color="text.secondary">
            Yalnız seçili rollerdeki kullanıcılar görür (+ sahibi ve admin).
          </Typography>
        </Box>
      )}
    </Box>
  );
}
