namespace Kds.Contracts.Reports;

/// <summary>Paylaşım: Özel = yalnız sahibi + admin; Ekip = tenant'taki herkes;
/// Roller (S27) = yalnız <c>PaylasimRolleri</c> listesindeki rollerin üyeleri (+ sahibi + admin).
/// "Ekip"in belirsiz küme olması S10'dan devreden borçtu — rol kümesiyle somutlandı. DB'de string.</summary>
public enum ReportSharing { Ozel = 0, Ekip, Roller }
