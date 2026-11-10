using System.Threading.Channels;
using Kds.Contracts.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;

namespace Kds.Logging;

/// <summary>Olay günlüğünü ayrı Mongo DB'ye (KdsLog) yazar. <see cref="Write"/> BLOKLAMAZ — olayı
/// sınırlı bir kanala bırakır; arka plan servisi kanalı boşaltıp koleksiyon başına (Action/Exception/
/// DataChange) toplu yazar. Mongo düşse bile istek etkilenmez (uyarı loglanır, olaylar düşer). Aynı
/// örnek hem <see cref="IEventLog"/> hem hosted service olarak kayıtlıdır → kanal paylaşılır.</summary>
public sealed class MongoEventLog : BackgroundService, IEventLog
{
    private readonly Channel<ILogEvent> _kanal;
    private readonly IMongoDatabase _db;
    private readonly ILogger<MongoEventLog> _log;

    // MongoDB.Driver 3.x Guid serileştirmesi için GuidRepresentation ZORUNLU (aksi hâlde
    // "GuidRepresentation is Unspecified" fırlatır). Standard (UUID subtype 4) — süreç genelinde bir kez.
    static MongoEventLog() => BsonSerializer.TryRegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));

    public MongoEventLog(IOptions<EventLogOptions> secenekler, ILogger<MongoEventLog> log)
    {
        _log = log;
        var o = secenekler.Value;
        _db = new MongoClient(o.ConnectionString).GetDatabase(o.Database);
        _kanal = Channel.CreateBounded<ILogEvent>(new BoundedChannelOptions(o.Capacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
        });
    }

    public void Write(ILogEvent olay) => _kanal.Writer.TryWrite(olay);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var tampon = new List<ILogEvent>(256);
        try
        {
            while (await _kanal.Reader.WaitToReadAsync(stoppingToken).ConfigureAwait(false))
            {
                tampon.Clear();
                while (tampon.Count < 256 && _kanal.Reader.TryRead(out var olay))
                    tampon.Add(olay);
                try
                {
                    await YazAsync(tampon, stoppingToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _log.LogWarning(ex, "Olay günlüğü Mongo yazımı başarısız — {Adet} olay düştü", tampon.Count);
                }
            }
        }
        catch (OperationCanceledException) { /* kapanış */ }
    }

    private async Task YazAsync(List<ILogEvent> olaylar, CancellationToken ct)
    {
        foreach (var grup in olaylar.GroupBy(e => e.Collection))
        {
            var koleksiyon = _db.GetCollection<BsonDocument>(grup.Key);
            var belgeler = grup.Select(ToDoc).ToList();
            await koleksiyon.InsertManyAsync(belgeler, new InsertManyOptions { IsOrdered = false }, ct)
                .ConfigureAwait(false);
        }
    }

    /// <summary>Olayı BsonDocument'e çevirir: <c>Id</c> → <c>_id</c> (Mongo yakınsaması), hesaplanan
    /// <c>Collection</c> ayırıcısı belgeye yazılmaz (koleksiyon zaten ayrı).</summary>
    private static BsonDocument ToDoc(ILogEvent olay)
    {
        var doc = olay.ToBsonDocument(olay.GetType());
        doc.Remove("Collection");
        return doc;
    }
}
