using FluentValidation;
using Kds.Business.Ingest;
using Kds.Contracts.Alerts;

namespace Kds.Business.Alerts;

/// <summary>Uyarı kaydetme doğrulaması — kardeşi <c>SaveSubscriptionValidation</c> ile aynı hat.
///
/// <para>İlk sürümde unutulmuştu ve sonucu sessizdi: <b>geçersiz cron kabul ediliyor</b>, uyarı
/// kaydediliyor ama zamanlayıcı ifadeyi ayrıştıramadığı için o uyarı HİÇ koşmuyordu — ekranda
/// "Aktif" görünen bir uyarı, sessizce ölü. Bu tür bir arıza, uyarı özelliğinde en pahalısıdır:
/// kullanıcı korunduğunu sanır.</para></summary>
public sealed class SaveAlertValidation : AbstractValidator<SaveAlertRequest>
{
    public SaveAlertValidation()
    {
        RuleFor(x => x.Ad).NotEmpty().MaximumLength(200);

        // S30: kaynak TAM BİRİ — rapor YA DA metrik. İkisi birden dolarsa hangisinin izlendiği
        // belirsiz kalır; ikisi de boşsa uyarının bakacağı yer yoktur.
        RuleFor(x => x)
            .Must(x => (x.RaporId is not null) ^ (x.MetrikId is not null))
            .WithMessage("Uyarı ya bir raporu ya bir metriği izler — tam birini seçin.");

        // SatirSayisi dışındaki özetler bir kolona bakar; kolonsuz kayıt her koşumda hata verirdi.
        // Metrik uyarısında kolon/özet zaten kullanılmaz — kural yalnız rapor yolunda.
        RuleFor(x => x.Kolon)
            .NotEmpty()
            .When(x => x.RaporId is not null && x.Ozet != OzetTuru.SatirSayisi)
            .WithMessage("Bu özet için izlenecek kolon seçilmeli (yalnız 'Satır sayısı' kolonsuz çalışır).");
        RuleFor(x => x.Kolon).MaximumLength(200);

        RuleFor(x => x.Ozet).IsInEnum();
        RuleFor(x => x.Op).IsInEnum();

        RuleFor(x => x.Alicilar).NotEmpty()
            .WithMessage("En az bir alıcı e-posta gerekli — alıcısız uyarı kimseye haber veremez.");
        RuleForEach(x => x.Alicilar).EmailAddress().WithMessage("Geçersiz e-posta adresi.");

        RuleFor(x => x.Zamanlama)
            .MaximumLength(100)   // DB kolonu nvarchar(100); sınırı DB hatası değil doğrulama söylesin
            .Must(z => string.IsNullOrWhiteSpace(z) || CronZamanlama.Gecerli(z))
            .WithMessage("Zamanlama geçerli bir cron ifadesi olmalı (5 alan, ya da saniyeli 6 alan).");
    }
}
