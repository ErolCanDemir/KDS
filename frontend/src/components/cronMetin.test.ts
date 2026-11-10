import { describe, expect, it } from 'vitest';
import { cronAciklama, cronCoz, cronKur } from './cronMetin';

// M22/M23: zamanlama seçici çevirileri.

describe('cronCoz + cronKur', () => {
  it('boş = Elle; kur geri boş üretir', () => {
    expect(cronCoz('').mod).toBe('Elle');
    expect(cronKur({ mod: 'Elle', dakika: 0, saat: 3, gun: 1, ozel: '' })).toBe('');
  });

  it('günlük/haftalık/saatlik gidiş-dönüş kayıpsız', () => {
    for (const c of ['0 3 * * *', '30 18 * * *', '0 * * * *', '15 * * * *', '0 6 * * 1', '45 23 * * 0']) {
      expect(cronKur(cronCoz(c))).toBe(c);
    }
  });

  it('tanınmayan desen Ozel olur ve olduğu gibi korunur', () => {
    const s = cronCoz('*/5 * * * *');
    expect(s.mod).toBe('Ozel');
    expect(cronKur(s)).toBe('*/5 * * * *');
  });
});

describe('cronAciklama', () => {
  it('Türkçe, insan-okur özet üretir', () => {
    expect(cronAciklama('')).toContain('elle');
    expect(cronAciklama('0 3 * * *')).toBe("Her gün saat 03:00'de çalışır.");
    expect(cronAciklama('0 6 * * 1')).toBe("Her Pazartesi saat 06:00'de çalışır.");
    expect(cronAciklama('15 * * * *')).toContain('15. dakikasında');
  });
});
