using FluentValidation;
using Kds.Business.Queries;
using Kds.Contracts.Queries;
using Kds.Common.Pipeline;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Kds.Business;

public static class BusinessRegistrations
{
    public static IServiceCollection AddKdsBusiness(this IServiceCollection servisler)
    {
        servisler.AddMediatR(ayar => ayar.RegisterServicesFromAssembly(typeof(BusinessRegistrations).Assembly));
        servisler.AddValidatorsFromAssembly(typeof(BusinessRegistrations).Assembly, includeInternalTypes: true);
        servisler.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        // Sorgu yürütmeye özel denetim (audit) davranışı — kapalı-tip pipeline behavior.
        servisler.AddTransient<IPipelineBehavior<RunQueryRequest, QueryRunResult>, QueryAuditBehavior>();
        // Şema keşfi handler'larının paylaştığı bağlantı-dizesi çözücü (parola çöz + connectionString kur).
        servisler.AddScoped<DataMaps.ConnectionStringResolver>();
        // Koşum + tanılama (M33) paylaşımı: mod yönlendirmeli (Canlı/İçe Alım/depo hedefi) sorgu dizesi.
        servisler.AddScoped<QueryConnectionResolver>();
        // S16/A3: Sistem Ayarları (zaman aşımı, limitler, tanı eşikleri) — koddaki sabitlerin yerine.
        servisler.AddScoped<Settings.ISistemAyarlari, Settings.SistemAyarlari>();
        // RLS — satır-düzeyi güvenlik çözücüsü (kural depolamasından okur; her koşumda çalışır).
        servisler.AddScoped<Security.ISatirKuraliCozucu, Security.SatirKuraliCozucu>();
        // Depo sunucusu/DB adı: Sistem Ayarları → kurulum sırası tek yerde çözülür.
        servisler.AddScoped<Ingest.DepoCozucu>();
        // Harita okuma servisi: HaritaGetir ucu + sorgu handler'larının paylaştığı tek okuma yolu
        // (handler içinden handler Send'lenmez — tek istisna bilinçli tek-kapı RunQueryRequest).
        servisler.AddScoped<DataMaps.IDataMapReader, DataMaps.DataMapReader>();
        // S22 Metrikler: modeldeki metrik referanslarini katalogdan formule cozer (her kosumda).
        servisler.AddScoped<Metrics.IMetrikCozucu, Metrics.MetrikCozucu>();
        return servisler;
    }
}
