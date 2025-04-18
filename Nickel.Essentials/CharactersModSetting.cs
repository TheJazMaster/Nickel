using FSPRO;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Nickel.Essentials;

public sealed class CharactersModSetting : IModSettingsApi.IModSetting
{
	public UIKey Key { get; private set; }
	public event IModSettingsApi.OnMenuOpen? OnMenuOpen;
	public event IModSettingsApi.OnMenuClose? OnMenuClose;
	private IModSettingsApi.IModSettingsRoute CurrentRoute = null!;

	public required Func<string> Title { get; set; }
	public required Func<List<Deck>> AllCharacters { get; set; }
	public required Func<Deck, bool> IsSelected { get; set; }
	public required Action<IModSettingsApi.IModSettingsRoute, Deck, bool> SetSelected { get; set; }
	public Func<IEnumerable<Tooltip>>? Tooltips { get; set; }
	public Func<Deck, IEnumerable<Tooltip>>? CharacterTooltips { get; set; }

	private UIKey BaseCharacterKey;

	public CharactersModSetting()
	{
		this.OnMenuOpen += (_, route) =>
		{
			if (this.Key == 0)
				this.Key = ModEntry.Instance.Helper.Utilities.ObtainEnumCase<UK>();
			if (this.BaseCharacterKey == 0)
				this.BaseCharacterKey = ModEntry.Instance.Helper.Utilities.ObtainEnumCase<UK>();
			this.CurrentRoute = route;
		};
	}

	~CharactersModSetting()
	{
		if (this.Key != 0)
			ModEntry.Instance.Helper.Utilities.FreeEnumCase(this.Key.k);
		if (this.BaseCharacterKey != 0)
			ModEntry.Instance.Helper.Utilities.FreeEnumCase(this.BaseCharacterKey.k);
	}

	public void RaiseOnMenuOpen(G g, IModSettingsApi.IModSettingsRoute route)
		=> this.OnMenuOpen?.Invoke(g, route);

	public void RaiseOnMenuClose(G g)
		=> this.OnMenuClose?.Invoke(g);

	public Vec? Render(G g, Box box, bool dontDraw)
	{
		const int width = 35;
		const int height = 33;
		
		var perRow = (int)((box.rect.w - 20) / width);
		var rows = this.AllCharacters().Chunk(perRow).ToList();

		if (!dontDraw)
		{
			Draw.Text(this.Title(), box.rect.x + 10, box.rect.y + 5, DB.thicket, Colors.textMain);

			if (this.Tooltips is { } tooltips && box.key is not null && box.IsHover())
				g.tooltips.Add(new Vec(box.rect.x2 - Tooltip.WIDTH, box.rect.y2), tooltips());

			for (var y = 0; y < rows.Count; y++)
			{
				var row = rows[y];
				for (var x = 0; x < row.Length; x++)
				{
					var deck = row[x];
					var character = new Character { type = deck.Key(), deckType = deck };
					var characterUiKey = new UIKey(this.BaseCharacterKey.k, (int)deck, character.type);
					
					character.Render(g, 10 + x * width, 20 + y * height, mini: true, isSelected: this.IsSelected(deck), onMouseDown: new MouseDownHandler(() =>
					{
						Audio.Play(Event.Click);
						this.SetSelected(this.CurrentRoute, deck, !this.IsSelected(deck));
					}), overrideKey: characterUiKey);

					if (this.CharacterTooltips is { } characterTooltips && g.boxes.FirstOrDefault(b => b.key == characterUiKey) is { } characterBox && characterBox.IsHover())
						g.tooltips.Add(g.tooltips.pos, characterTooltips(deck));
				}
			}
		}

		return new(box.rect.w, 20 + rows.Count * height);
	}
}
