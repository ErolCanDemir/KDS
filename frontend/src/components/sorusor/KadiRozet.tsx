import Box from '@mui/material/Box';

/** Sohbetteki Kadi avatarı — asistan kimliği ekranın İÇİNE girer (sağ alttaki 3D beden yerinde
 * kalır; bu yalnız "bu satır Kadi'nin" işareti). Saf CSS: sohbet listesinde onlarca kez
 * render edildiği için ikinci bir WebGL bağlamı açmak pahalı olurdu. */
export function KadiRozet({ dusunuyor, uzgun }: { dusunuyor?: boolean; uzgun?: boolean }) {
  const gozRengi = uzgun ? '#d64545' : dusunuyor ? 'var(--kds-mor)' : '#2fd6e8';
  return (
    <Box sx={{
      width: 28, height: 28, borderRadius: '50%', flex: '0 0 auto', position: 'relative',
      background: 'linear-gradient(160deg,#f4f7fa,#c3ced9)', border: '1.5px solid #9fb3c4',
      display: 'flex', alignItems: 'center', justifyContent: 'center',
    }}>
      {/* vizör */}
      <Box sx={{ width: 16, height: 10, borderRadius: '5px', bgcolor: '#101b24' }} />
      {/* gözler */}
      <Box sx={{
        position: 'absolute', width: 4.5, height: 4.5, borderRadius: '50%', bgcolor: gozRengi,
        boxShadow: `7px 0 0 ${gozRengi}`, ml: '-5px',
        animation: dusunuyor ? 'kds-kadi-nabiz 1.3s ease-in-out infinite' : 'none',
      }} />
    </Box>
  );
}
