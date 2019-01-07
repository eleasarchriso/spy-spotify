using EspionSpotify.Models;
using SpotifyAPI.Web;
using SpotifyAPI.Web.Auth;
using SpotifyAPI.Web.Enums;
using SpotifyAPI.Web.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EspionSpotify.MediaTags
{
    public class SpotifyApi : IExternalAPI
    {
        public string _clientId;
        public string _secretId;

        private Token _token;
        private DateTimeOffset _nextTokenRenewal;
        private AuthorizationCodeAuth _authorizationCodeAuth;
        private readonly LastFMAPI _lastFmApi = new LastFMAPI();

        /// <summary>
        /// Go to https://developer.spotify.com/dashboard/applications/ and register a new application
        /// Get the "Client ID" and "Client Secret" from that newly created application.
        /// Edit the settings and set as "Redirect URI" the value "http://localhost:4002"
        /// </summary>
        public SpotifyApi(string clientId, string secretId, string redirectUrl = "http://localhost:4002")
        {
            _clientId = clientId;
            _secretId = secretId;

            if (!string.IsNullOrEmpty(_clientId) && !string.IsNullOrEmpty(_secretId))
            {
                var auth = new AuthorizationCodeAuth(_clientId, _secretId, redirectUrl, redirectUrl,
                    Scope.Streaming | Scope.PlaylistReadCollaborative | Scope.UserReadCurrentlyPlaying | Scope.UserReadRecentlyPlayed | Scope.UserReadPlaybackState);
                auth.AuthReceived += AuthOnAuthReceived;
                auth.Start();
                auth.OpenBrowser();
            }
        }

        public void UpdateInfo(Track track)
        {
            var api = GetSpotifyWebAPI().Result;

            if (api == null) return;

            var playback = api.GetPlayback();

            if (playback.HasError() || playback.Item == null)
            {
                // fallback in case getting the playback did not work
                _lastFmApi.UpdateInfo(track);
                return;
            }

            track.Title = playback.Item.Name;
            track.AlbumPosition = playback.Item.TrackNumber;
            track.Performers = playback.Item.Artists?.Select(a => a.Name).ToArray();
            track.Disc = (uint)playback.Item.DiscNumber;

            if (playback?.Item?.Album?.Id == null) return;

            var album = api.GetAlbum(playback.Item.Album.Id);

            if (album.HasError()) return;

            track.AlbumArtists = album.Artists.Select(a => a.Name).ToArray();
            track.Album = album.Name;
            track.Genres = album.Genres.ToArray();
            if (uint.TryParse(album.ReleaseDate?.Substring(0, 4), out var year))
            {
                track.Year = year;
            }

            if (album.Images.Count > 0)
            {
                var sorted = album.Images.OrderByDescending(i => i.Width).ToList();

                if (sorted.Count > 0) track.ArtExtraLargeUrl = sorted[0].Url;
                if (sorted.Count > 1) track.ArtLargeUrl = sorted[1].Url;
                if (sorted.Count > 2) track.ArtMediumUrl = sorted[2].Url;
                if (sorted.Count > 3) track.ArtSmallUrl = sorted[3].Url;
                if (sorted.Count > 4) track.ArtExtraLargeUrl = sorted[4].Url;
            }
        }

        private async void AuthOnAuthReceived(object sender, AuthorizationCode payload)
        {
            _authorizationCodeAuth = (AuthorizationCodeAuth)sender;
            _authorizationCodeAuth.Stop();

            _token = await _authorizationCodeAuth.ExchangeCode(payload.Code);
            
            // remember when to renew the token (one minute upfront)
            _nextTokenRenewal = DateTimeOffset.UtcNow.AddSeconds(_token.ExpiresIn).AddMinutes(-1);
        }

        private async Task<SpotifyWebAPI> GetSpotifyWebAPI()
        {
            if (_token == null) return null;

            if (DateTimeOffset.UtcNow >= _nextTokenRenewal)
            {
                _token = await _authorizationCodeAuth.RefreshToken(_token.RefreshToken);
            }

            return new SpotifyWebAPI
            {
                AccessToken = _token.AccessToken,
                TokenType = _token.TokenType
            };
        }
    }
}
