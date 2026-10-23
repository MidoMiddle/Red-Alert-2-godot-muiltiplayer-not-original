using Rts.Client.Audio;

namespace Rts.Simulation.Tests.Client;

/// <summary>
/// The players of the sounds (docs/implementation/M6_menus_skirmish.md, 4.8, section 6): a sound gives way to a more important one,
/// the quietest of the least important gives way first, and the limits of a sound and its cooldown hold.
/// </summary>
public sealed class VoicePoolTests
{
    private const int Unlimited = 100;

    private static VoiceRequest Shot(float loudness = 1f, int maxConcurrent = Unlimited, int cooldownMs = 0) =>
        new("snd_shot", SoundPriority.Shot, loudness, maxConcurrent, cooldownMs);

    private static VoiceRequest Explosion(float loudness = 1f) => new("snd_explosion", SoundPriority.Explosion, loudness, Unlimited, 0);

    private static VoiceRequest Ambience(float loudness = 1f) => new("snd_ambience", SoundPriority.Ambience, loudness, Unlimited, 0);

    [Fact]
    public void A_free_player_is_taken_without_eviction()
    {
        var pool = new VoicePool(2);

        Assert.True(pool.TryAcquire(Shot(), 0, out var grant));

        Assert.Equal(-1, grant.Evicted);
        Assert.Equal(1, pool.Active);
    }

    [Fact]
    public void A_released_player_is_taken_again()
    {
        var pool = new VoicePool(1);
        Assert.True(pool.TryAcquire(Shot(), 0, out var first));

        pool.Release(first.Slot);

        Assert.True(pool.TryAcquire(Shot(), 1000, out var second));
        Assert.Equal(first.Slot, second.Slot);
        Assert.Equal(1, pool.Active);
    }

    [Fact]
    public void A_more_important_sound_evicts_the_least_important_one()
    {
        var pool = new VoicePool(2);
        Assert.True(pool.TryAcquire(Ambience(), 0, out var ambience));
        Assert.True(pool.TryAcquire(Shot(), 0, out _));

        Assert.True(pool.TryAcquire(Explosion(), 0, out var grant));

        Assert.Equal(ambience.Slot, grant.Evicted);
        Assert.Equal(ambience.Slot, grant.Slot);
    }

    [Fact]
    public void Among_busy_players_the_least_important_gives_way_before_the_quietest()
    {
        var pool = new VoicePool(2);
        Assert.True(pool.TryAcquire(Shot(loudness: 1f), 0, out _));
        Assert.True(pool.TryAcquire(Ambience(loudness: 0.1f), 0, out var ambience));

        Assert.True(pool.TryAcquire(Explosion(), 0, out var grant));

        Assert.Equal(ambience.Slot, grant.Evicted);
    }

    [Fact]
    public void Among_equal_sounds_the_quietest_gives_way_to_a_louder_one()
    {
        var pool = new VoicePool(2);
        Assert.True(pool.TryAcquire(Shot(loudness: 0.2f), 0, out var quiet));
        Assert.True(pool.TryAcquire(Shot(loudness: 0.9f), 0, out _));

        Assert.True(pool.TryAcquire(Shot(loudness: 0.5f), 0, out var grant));

        Assert.Equal(quiet.Slot, grant.Evicted);
    }

    [Fact]
    public void A_quieter_sound_does_not_evict_an_equal_one()
    {
        var pool = new VoicePool(2);
        Assert.True(pool.TryAcquire(Shot(loudness: 0.5f), 0, out _));
        Assert.True(pool.TryAcquire(Shot(loudness: 0.9f), 0, out _));

        Assert.False(pool.TryAcquire(Shot(loudness: 0.1f), 0, out _));
        Assert.Equal(2, pool.Active);
    }

    [Fact]
    public void A_less_important_sound_is_refused_when_every_player_is_busy()
    {
        var pool = new VoicePool(1);
        Assert.True(pool.TryAcquire(Explosion(), 0, out _));

        Assert.False(pool.TryAcquire(Shot(loudness: 1f), 0, out _));
    }

    [Fact]
    public void The_limit_of_simultaneous_sounds_refuses_the_extra_one_even_with_room_left()
    {
        var pool = new VoicePool(10);
        Assert.True(pool.TryAcquire(Shot(maxConcurrent: 2), 0, out _));
        Assert.True(pool.TryAcquire(Shot(maxConcurrent: 2), 0, out _));

        Assert.False(pool.TryAcquire(Shot(maxConcurrent: 2), 0, out _));
        Assert.True(pool.TryAcquire(Explosion(), 0, out _));
        Assert.Equal(2, pool.ActiveOf("snd_shot"));
    }

    [Fact]
    public void The_cooldown_refuses_a_sound_that_started_too_recently()
    {
        var pool = new VoicePool(4);
        Assert.True(pool.TryAcquire(Shot(cooldownMs: 40), 0, out _));

        Assert.False(pool.TryAcquire(Shot(cooldownMs: 40), 39, out _));
        Assert.True(pool.TryAcquire(Shot(cooldownMs: 40), 40, out _));
    }

    [Fact]
    public void A_refused_sound_does_not_start_a_cooldown()
    {
        var pool = new VoicePool(1);
        Assert.True(pool.TryAcquire(Explosion(), 0, out var blast));
        Assert.False(pool.TryAcquire(Shot(cooldownMs: 40), 0, out _));

        pool.Release(blast.Slot);

        Assert.True(pool.TryAcquire(Shot(cooldownMs: 40), 1, out _));
    }
}
