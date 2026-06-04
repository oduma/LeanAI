using Android.App;
using Android.Content;
using Android.Views;
using Android.Widget;
using LeanAI.Application.ActivityTracking.Commands.AnalyzeRunImage;
using LeanAI.Application.ActivityTracking.DTOs;
using LeanAI.Application.FoodTracking.Commands.AnalyzeFoodImage;
using LeanAI.Application.Shared;
using LeanAI.Application.Shared.Commands.ClassifyShareImage;
using LeanAI.Infrastructure;
using LeanAI.Infrastructure.ActivityTracking.Services;
using LeanAI.Infrastructure.FoodTracking.Services;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui;

namespace LeanAI.Maui.Platforms.Android;

[Activity(Label = "Share with LeanAI", Exported = true)]
[IntentFilter(
    new[] { global::Android.Content.Intent.ActionSend },
    Categories = new[] { global::Android.Content.Intent.CategoryDefault },
    DataMimeType = "image/*",
    Label = "Share with LeanAI")]
public class ShareWithLeanAIActivity : Activity
{
    private const string GeminiKeyStorageKey = "gemini_key";

    protected override void OnCreate(global::Android.OS.Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        SetLoadingView();

#pragma warning disable CA1422
        var imageUri = Intent?.GetParcelableExtra(
            global::Android.Content.Intent.ExtraStream) as global::Android.Net.Uri;
#pragma warning restore CA1422

        if (imageUri is null)
        {
            Finish();
            return;
        }

        var mimeType = ContentResolver!.GetType(imageUri) ?? "image/jpeg";

        byte[] imageBytes;
        using (var stream = ContentResolver.OpenInputStream(imageUri)!)
        using (var ms = new MemoryStream())
        {
            stream.CopyTo(ms);
            imageBytes = ms.ToArray();
        }

        var services         = IPlatformApplication.Current!.Services;
        var mediator         = services.GetRequiredService<IMediator>();
        var foodImportState  = services.GetRequiredService<FoodImportStateService>();
        var runImportState   = services.GetRequiredService<RunImportStateService>();

        Task.Run(async () =>
        {
            try
            {
                var apiKey = await SecureStorage.GetAsync(GeminiKeyStorageKey);
                if (!string.IsNullOrEmpty(apiKey))
                    services.GetRequiredService<GeminiKeyHolder>().ApiKey = apiKey;

                var imageType = await mediator.Send(
                    new ClassifyShareImageCommand(imageBytes, mimeType));

                switch (imageType)
                {
                    case ShareImageType.Food:
                    {
                        var date  = DateOnly.FromDateTime(DateTime.Today);
                        var items = await mediator.Send(
                            new AnalyzeFoodImageCommand(imageBytes, mimeType, date));

                        foodImportState.Set(items);

                        var intent = new Intent(this, typeof(MainActivity));
                        intent.AddFlags(ActivityFlags.NewTask | ActivityFlags.SingleTop);
                        StartActivity(intent);
                        RunOnUiThread(Finish);
                        break;
                    }

                    case ShareImageType.Run:
                    {
                        var result = await mediator.Send(
                            new AnalyzeRunImageCommand(imageBytes, mimeType));

                        var row = new RunActivityRowDto(
                            result.ActivityText,
                            result.CaloriesBurned,
                            IsRunRow: true,
                            result.Metrics);

                        runImportState.Set([row]);

                        var intent = new Intent(this, typeof(MainActivity));
                        intent.AddFlags(ActivityFlags.NewTask | ActivityFlags.SingleTop);
                        StartActivity(intent);
                        RunOnUiThread(Finish);
                        break;
                    }

                    default:
                        RunOnUiThread(() =>
                        {
                            Toast.MakeText(
                                    this,
                                    "Couldn't identify this image. Share a food photo or a run screenshot.",
                                    ToastLength.Long)!
                                 .Show();
                            Finish();
                        });
                        break;
                }
            }
            catch
            {
                RunOnUiThread(() =>
                {
                    Toast.MakeText(
                            this,
                            "Could not process the image — please try again.",
                            ToastLength.Long)!
                         .Show();
                    Finish();
                });
            }
        });
    }

    private void SetLoadingView()
    {
        var dp = Resources!.DisplayMetrics!.Density;

        var colorBase   = global::Android.Graphics.Color.ParseColor("#222222");
        var colorText   = global::Android.Graphics.Color.ParseColor("#F0F2F5");
        var colorCopper = global::Android.Graphics.Color.ParseColor("#D28B5C");
        var colorNickel = global::Android.Graphics.Color.ParseColor("#9A9EAB");

        var root = new LinearLayout(this) { Orientation = Orientation.Vertical };
        root.SetGravity(GravityFlags.Center);
        root.SetBackgroundColor(colorBase);
        root.LayoutParameters = new ViewGroup.LayoutParams(
            ViewGroup.LayoutParams.MatchParent,
            ViewGroup.LayoutParams.MatchParent);

        var appName = new TextView(this)
        {
            Text     = "LeanAI",
            TextSize = 26f,
            Gravity  = GravityFlags.Center
        };
        appName.SetTextColor(colorText);
        appName.SetTypeface(null, global::Android.Graphics.TypefaceStyle.Bold);
        var nameLp = new LinearLayout.LayoutParams(
            LinearLayout.LayoutParams.WrapContent,
            LinearLayout.LayoutParams.WrapContent);
        nameLp.BottomMargin = (int)(32 * dp);
        root.AddView(appName, nameLp);

        var spinner = new global::Android.Widget.ProgressBar(this, null,
            global::Android.Resource.Attribute.ProgressBarStyleLarge)
        {
            Indeterminate = true
        };
        spinner.IndeterminateTintList =
            global::Android.Content.Res.ColorStateList.ValueOf(colorCopper);
        var spinnerLp = new LinearLayout.LayoutParams(
            LinearLayout.LayoutParams.WrapContent,
            LinearLayout.LayoutParams.WrapContent);
        spinnerLp.BottomMargin = (int)(20 * dp);
        root.AddView(spinner, spinnerLp);

        var label = new TextView(this)
        {
            Text     = "Analysing your image…",
            TextSize = 15f,
            Gravity  = GravityFlags.Center
        };
        label.SetTextColor(colorNickel);
        root.AddView(label);

        SetContentView(root);
    }
}
