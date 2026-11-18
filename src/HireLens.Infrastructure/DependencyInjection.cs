using HireLens.Application.Analysis;
using HireLens.Infrastructure.Extraction;
using HireLens.Infrastructure.Providers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HireLens.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddHireLensAnalysis(this IServiceCollection s, IConfiguration cfg)
    {
        s.AddSingleton<ITextExtractor, TextExtractor>();
        if (string.IsNullOrWhiteSpace(cfg["Anthropic:ApiKey"]))
            s.AddSingleton<ICvAnalysisProvider, MockCvAnalysisProvider>();
        else
            s.AddHttpClient<ICvAnalysisProvider, ClaudeCvAnalysisProvider>(c => c.Timeout = TimeSpan.FromSeconds(90));
        s.AddScoped<CvAnalysisService>();
        return s;
    }
}
