# Localization From Source

## What it does

The traditional way to localize a Stardew Valley mod involves finding every localizable string (by eye), copying them to
the `default.json` file (while being careful to escape for json), inventing a key, and changing the code to use that
key...  and then retesting the entire mod carefully because you could have goofed up any of those manual steps.
With this system, you just mark all your localizable strings with an `L`, like so:

```C#
ModEntry.AddQuestItem(
    objects,
    OldJunimoPortalQiid,
    L("a strange little structure"),
    L("At first it looked like a woody weed, but a closer look makes it like a little structure, and it smells sorta like the Wizard's forest-magic potion."),
    0);
```

A compile-time step takes care of populating the default.json file.  Note that the `L` isn't magic, it works
only if you have this line in your using block:

```C#
using static LocalizeFromSourceLib.LocalizeMethods;
```

## How it helps Translators

Now let's talk about what happens after someone creates a localized version of your default.json.  It's certainly easy
to do on the first go-around.  They just copy/paste your default.json into `de.json` and convert all the English strings
within it to German.  Where problems start is when you update a string or add a new one.  Then you have to get your
German translator back on-line and have them diff your default.json file (which may not be easy for a non-technical
person to do), and produce the necessary changes.

This package automates that by maintaining the `de.json` file for you (doing things like deleting keys out of there when
you delete strings from the code) and it also produces a `de.edits.json` file that spells out exactly what changed
for the translators.  For each change, it shows the old source-language string, a github link to the code where the
string was found, the string's new value, and the old translation.  It leaves a spot for the translator to fill in
the new value so all they have to do is edit that one file.  When you rebuild with the updated `de.edits.json` file,
it will move those strings into the `de.json` file and clean up the `de.edits.json` file once everything lines up.

This doesn't just aim at making it easier for the localizer to keep translations current - it aims at making it more
likely that they will do it.  As it stands now, what tends to happen is that a multilingual player sees your mod,
likes it, enthuses over it, asks to do a translation so that their friends can play it too, then disappears entirely.
New multi-lingual players appear all the time, but it's pretty daunting to chime in and offer to update the
translations - what state was it left in?  When was it last translated?  What strings are actually broken?
Will the previous translator ever reappear?  All these questions and more make it hard for people to step in
and fix things.  If there's a single file they can look at on GitHub that tells them exactly what needs updated,
they can just update that file and mail you the update and leave it up to you to apply it.

## Mixed code and localizable strings

In Stardew Valley, there are cases where what amounts to game code is mixed into a localizable string.

```C#
            else if (e.NameWithoutLocale.IsEquivalentTo("Data/Quests"))
            {
                e.Edit(editor =>
                {
                    IDictionary<string, string> data = editor.AsDictionary<string, string>().Data;
                    data[MeetLinusAtTentQuest] = "Basic/Find Linus At His Tent/Linus said he had something he needed your help with./Go to Linus' tent before 10pm/null/-1/0/-1/false";
                    data[MeetLinusAt60Quest] = "Basic/Meet Linus At Level 60/Linus had something he wanted to show you at level 60 of the mines./Follow Linus to level 60/null/-1/0/-1/false";
                    data[CatchIcePipsQuest] = "Basic/Catch Six Ice Pips/Catch six ice pips and put them in the mysterious fish tank.//null/-1/0/-1/false";
                });
            }
```

With this system, you *could* just wrap those strings in `L()` and call it a day.  At that point you'd be pretty
much at the same point as if you used the traditional localization system.  With this package, you can move it one step farther and wrap it in `SdvQuest`, like so:

```C#
            else if (e.NameWithoutLocale.IsEquivalentTo("Data/Quests"))
            {
                e.Edit(editor =>
                {
                    IDictionary<string, string> data = editor.AsDictionary<string, string>().Data;
                    data[MeetLinusAtTentQuest] = SdvQuest("Basic/Find Linus At His Tent/Linus said he had something he needed your help with./Go to Linus' tent before 10pm/null/-1/0/-1/false");
                    data[MeetLinusAt60Quest] = SdvQuest("Basic/Meet Linus At Level 60/Linus had something he wanted to show you at level 60 of the mines./Follow Linus to level 60/null/-1/0/-1/false");
                    data[CatchIcePipsQuest] = SdvQuest("Basic/Catch Six Ice Pips/Catch six ice pips and put them in the mysterious fish tank.//null/-1/0/-1/false");
                });
            }
```

That tells the system that the string is a quest, and it will actually add just the localizable parts to `default.json`.
That is, in this case, it wouldn't add `"key": "Basic/Find Linus...`, it will add `"key1": "Find Linus At His Tent"` and
`"key2": "Linus said...` and so on.  The win here is that if you ever have to update something in the non-localizable
part of the string, you can do so *without* having to make the same change across all your languages.

For quests, that's nice.  But there's also a `SdvEvent` method, and, well, I think the power of that is self-evident.

## Ensuring everything that should be localized is localized

Generally, you can eyeball when a string should be localized and you can do a pretty good job of finding them.
But if "pretty good" isn't what you're after and you want static analysis to help ensure that you've got everything,
this package can do that.  Turn on `strict` mode by adding a `LocalizeFromSource.json` file to your project:

```C#
{
    "isStrict": true,
    "invariantStringPatterns": [
        // dot and slash-separated identifiers
        "^\\w+[\\./\\\\_][\\w\\./\\\\_]*\\w$",

        // CamelCase or pascalCase identifiers - identified by all letters and digits with a lowercase letter right before an uppercase one.
        "^\\w*[\\p{Ll}\\d][\\p{Lu}]\\w*$",

        // Qualified item id's (O)blah.blah or (BC)Chest
        "^\\([A-Z]+\\)[\\p{L}\\d\\.]+$"
    ],
    "invariantMethods": [
        "StardewValley.Farmer.getFriendshipHeartLevelForNPC",
        "StardewValley.Game1.playSound"
    ]
}
```

The other properties assist in weeding out false-positives.  `invariantStringPatterns` allows you to describe strings that can be mechanically identified as non-localizable.  `invariantMethods` allow you to list methods that take
string identifiers as arguments.

> Note: by the time you read this, these patterns will probably be defaults, so you won't want to use these
> patterns just as-such.  But you probably will find SDV calls in your code that you commonly use that you'll
> want to add here.

There will always be cases that you can't mechanically identify or it's just not
worth the hassle of editing the json, for those cases, you can use `I()`, similar to `L`:

```C#
var c = farmAnimals.Values.Count(a => a.type.Value.EndsWith(I(" Chicken")));
```

## Format Strings

The perils of `String.Format(identifier, x, y)` have been known for a long time, and a number of remedies have
been devised, with varying degrees of effectiveness.  Interpolated strings is certainly one of the most powerful
of them, and this package aims to exploit them.  Alas, it can't quite be done seamlessly, you have to use `LF`
instead of just `L`.  Like so:

```C#
quest.currentObjective = LF($"{count} of 6 teleported");
```

If you do that, then you'll see a string like this generated in your default.json `"{arg0} of 6 teleported"`.

Note that there is also a version for invariant strings, `IF`.  Why can't we just do `L`?  It's a long story
involving how the compiler works.  If left to its own devices, $"{count} of 6 teleported" will get turned
into instructions that look a lot like allocating a `StringBuilder` and appending the count and the string to it.
If, on the other hand, you pass an interpolated string to a method that takes a `System.FormattableString` as
an argument, then it constructs such an object and passes that to the method.  That's the behavior that
`LF` is counting on.  "Aha!"  I hear you say.  "Just have an overload of `L` that takes a string and another
that takes `FormattableString`!  Problem solved!"  Ah, would that it were true.  If you do that, you'll find
that the `FormattableString` overload never gets called.  That's because the actual type of the object that
the compiler generates is not actually `FormattableString` but instead is a subclass, which is internal,
and there is an operator that can convert `FormattableString` to a string.  So the compiler has two choices,
where both involve a conversion.  It prefers the conversion to string and so, well, game over.  (If you know
a way to beat that, please raise an Issue!)



