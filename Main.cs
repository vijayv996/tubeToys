using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using Wox.Infrastructure;
using Wox.Plugin;
using Wox.Plugin.Logger;
using BrowserInfo = Wox.Plugin.Common.DefaultBrowserInfo;
using YTSearch;
using YTSearch.NET;

namespace Community.PowerToys.Run.Plugin.tubeToys
{
    public class Main : IPlugin, IPluginI18n, IContextMenu, ISettingProvider, IReloadable, IDisposable, IDelayedExecutionPlugin
    {
        private const string Setting = nameof(Setting);

        // current value of the setting
        private bool _setting;

        private PluginInitContext _context;

        private string _iconPath;

        private bool _disposed;

        private bool showViews {  get; set; }

        private bool showAuthor {  get; set; }

        private bool showLength { get; set; }

        public string Name => Properties.Resources.plugin_name;

        public string Description => Properties.Resources.plugin_description;

        // TODO: remove dash from ID below and inside plugin.json
        public static string PluginID => "69fd1e6891b040be92156c25b4e86aa2";

        // TODO: add additional options (optional)
        public IEnumerable<PluginAdditionalOption> AdditionalOptions => new List<PluginAdditionalOption>()
        {
            new()
            {
                Key = nameof(showViews),
                DisplayLabel = "Hide Views",
                PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Checkbox,
                Value = showViews,
            },
            new()
            {
                Key = nameof(showAuthor),
                DisplayLabel = "Hide Author(Channel)",
                PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Checkbox,
                Value = showAuthor,
            },
            new()
            {
                Key = nameof(showLength),
                DisplayLabel = "Hide Video Length",
                PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Checkbox,
                Value = showLength,
            }
        };

        public void UpdateSettings(PowerLauncherPluginSettings settings)
        {
            showViews = settings.AdditionalOptions.SingleOrDefault(x => x.Key == nameof(showViews))?.Value ?? false;
            showAuthor = settings.AdditionalOptions.SingleOrDefault(x => x.Key == nameof(showAuthor))?.Value ?? false;
            showLength = settings.AdditionalOptions.SingleOrDefault(x => x.Key == nameof(showLength))?.Value ?? false;
        }

        // TODO: return context menus for each Result (optional)
        public List<ContextMenuResult> LoadContextMenus(Result selectedResult)
        {
            return new List<ContextMenuResult>(0);
        }

        // TODO: return query results
        public List<Result> Query(Query query)
        {
            ArgumentNullException.ThrowIfNull(query);

            var results = new List<Result>();

            // empty query
            if (string.IsNullOrEmpty(query.Search))
            {
                results.Add(new Result
                {
                    Title = "Search",
                    SubTitle = Description,
                    QueryTextDisplay = string.Empty,
                    IcoPath = _iconPath,
                    Action = action =>
                    {
                        if (!Helper.OpenCommandInShell(BrowserInfo.Path, BrowserInfo.ArgumentsPattern, "https://www.youtube.com/"))
                        {
                            return false;
                        }
                        return true;
                    },
                });
                return results;
            }
            else
            {
                string searchTerm = query.Search;

                var result = new Result
                {
                    Title = searchTerm,
                    SubTitle = string.Format(CultureInfo.CurrentCulture, BrowserInfo.Name ?? BrowserInfo.MSEdgeName),
                    QueryTextDisplay = searchTerm,
                    IcoPath = _iconPath,
                };

                string arguments = $"https://www.youtube.com/results?search_query={searchTerm}";

                result.ProgramArguments = arguments;
                result.Action = action =>
                {
                    if (!Helper.OpenCommandInShell(BrowserInfo.Path, BrowserInfo.ArgumentsPattern, arguments))
                    {
                        return false;
                    }
                    return true;
                };

                results.Add(result);
            }

            return results;
        }

        public async Task<List<Result>> Scrape(string searchTerm, bool delayedExecution)
        {

            var results = new List<Result>();

            var client = new YouTubeSearchClient();
            var ytresults = (await client.SearchYoutubeVideoAsync(searchTerm)).Results;

            foreach(var item in ytresults.Take(4)) {

                string subTitle = "";
                if (!showAuthor) {
                    subTitle = subTitle + $"{item.Author}";
                }
                if (!showLength) {
                    if (subTitle.Length > 0) {
                        subTitle = subTitle + $" | {item.Length:mm\\:ss}";
                    } else {
                        subTitle = $"{item.Length:mm\\:ss}";
                    }
                }
                if (!showViews) {
                    int views = (int)item.Views;
                    string formattedViews;
                    if (views > 1000 && views < 1000000) {
                        formattedViews = views.ToString("#,##0,K", CultureInfo.InvariantCulture);
                    } else if (views > 999999 && views < 1000000000) {
                        formattedViews = views.ToString("#,##0,,M", CultureInfo.InvariantCulture);
                    } else if (views > 999999999) {
                        formattedViews = views.ToString("#,##0,,,B", CultureInfo.InvariantCulture);
                    } else {
                        formattedViews = views.ToString("#,#", CultureInfo.InvariantCulture);
                    }
                    if (subTitle.Length > 0) {
                        subTitle = subTitle + " | " + formattedViews;
                    } else {
                        subTitle = formattedViews;
                    }

                }

                results.Add(new Result
                {
                    Title = item.Title,
                    SubTitle = subTitle,
                    IcoPath = _iconPath,
                    Action = Action =>
                    {
                        if (!Helper.OpenCommandInShell(BrowserInfo.Path, BrowserInfo.ArgumentsPattern, item.Url))
                        {
                            return false;
                        }
                        return true;
                    }
                });

            }

            return results;
        }

        // TODO: return delayed query results (optional)
        public List<Result> Query(Query query, bool delayedExecution)
        {
            ArgumentNullException.ThrowIfNull(query);

            var results = new List<Result>();

            // empty query
            if (string.IsNullOrEmpty(query.Search))
            {
                return results;
            }
            string searchTerm = query.Search;
            var task = Scrape(searchTerm, delayedExecution);
            task.Wait();
            results.AddRange(task.Result);

            return results;
        }

        public void Init(PluginInitContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _context.API.ThemeChanged += OnThemeChanged;
            UpdateIconPath(_context.API.GetCurrentTheme());
        }

        public string GetTranslatedPluginTitle()
        {
            return Properties.Resources.plugin_name;
        }

        public string GetTranslatedPluginDescription()
        {
            return Properties.Resources.plugin_description;
        }

        private void OnThemeChanged(Theme oldtheme, Theme newTheme)
        {
            UpdateIconPath(newTheme);
        }

        private void UpdateIconPath(Theme theme)
        {
            if (theme == Theme.Light || theme == Theme.HighContrastWhite)
            {
                _iconPath = "Images/tubeToys.light.png";
            }
            else
            {
                _iconPath = "Images/tubeToys.dark.png";
            }
        }

        public Control CreateSettingPanel()
        {
            throw new NotImplementedException();
        }

        public void ReloadData()
        {
            if (_context is null)
            {
                return;
            }

            UpdateIconPath(_context.API.GetCurrentTheme());
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                if (_context != null && _context.API != null)
                {
                    _context.API.ThemeChanged -= OnThemeChanged;
                }

                _disposed = true;
            }
        }
    }
}
