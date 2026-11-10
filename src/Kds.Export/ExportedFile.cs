namespace Kds.Export;

/// <summary>Üretilen dosya: ham içerik + MIME tipi + indirilecek ad.</summary>
public sealed record ExportedFile(byte[] Icerik, string IcerikTipi, string DosyaAdi);
