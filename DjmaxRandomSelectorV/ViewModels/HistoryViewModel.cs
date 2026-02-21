using Caliburn.Micro;
using DjmaxRandomSelectorV.Messages;
using DjmaxRandomSelectorV.Models;
using Dmrsv.RandomSelector;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace DjmaxRandomSelectorV.ViewModels
{
    public class HistoryViewModel : Screen, IHandle<PatternMessage>, IHandle<FilterOptionMessage>
    {
        private readonly IEventAggregator _eventAggregator;
        private readonly TrackDB _db;

        private int _number;
        private bool _showsStyle;

        public BindableCollection<HistoryItem> History { get; }

        public HistoryViewModel(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;
            _eventAggregator.SubscribeOnUIThread(this);
            _db = IoC.Get<TrackDB>();
            _number = 0;
            History = new BindableCollection<HistoryItem>();
            DisplayName = "HISTORY";
            SetShowsStyle(IoC.Get<Dmrsv3Configuration>().Mode);
        }

        private void AddItem(Pattern pattern)
        {
            _number++;
            var lang = IoC.Get<Dmrsv3Configuration>().GameLanguage;
            var track = _db.AllTrack.FirstOrDefault(t => t.Id == pattern.Info.Id);
            string displayTitle = track == null ? pattern.Info.Title : lang switch
            {
                GameLanguage.English or GameLanguage.Chinese => track.TitleEn ?? track.Title,
                GameLanguage.Japanese => track.TitleJa ?? track.TitleEn ?? track.Title,
                _ => track.Title
            };
            var historyItem = new HistoryItem()
            {
                Number = _number,
                Info = pattern.Info with { Title = displayTitle },
                Category = pattern.Info.Category.Split(':')[0],
                Style = _showsStyle ? pattern.Style : "FREE",
                Level = _showsStyle ? pattern.Level.ToString() : "-",
                Time = new Regex(Regex.Escape(" ")).Replace(DateTime.Now.ToString("g"), "\n", 1),
            };

            History.Insert(0, historyItem);
            if (History.Count > 10)
            {
                History.RemoveAt(10);
            }
        }

        public void ClearItems()
        {
            History.Clear();
            _number = 0;
        }

        private void SetShowsStyle(MusicForm musicForm)
        {
            _showsStyle = musicForm == MusicForm.Default;
        }

        public Task HandleAsync(PatternMessage message, CancellationToken cancellationToken)
        {
            AddItem(message.Item);
            return Task.CompletedTask;
        }
        
        public Task HandleAsync(FilterOptionMessage message, CancellationToken cancellationToken)
        {
            SetShowsStyle(message.MusicForm);
            return Task.CompletedTask;
        }
    }
}
