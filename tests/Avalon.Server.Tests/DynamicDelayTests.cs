using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using FluentAssertions;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Network;
using Xunit;

namespace Meesles.Avalon.Server.Tests;

public sealed class DynamicDelayTests {
  [Fact]
  public void ServerBaseline_RecoversAfterStartupSpikeDespiteEchoedClientReport() {
    var service = new ServerNetworkService();
    service.Initialize(new NullTransport(), new CommandFactory(), null);
    Set(service, "_simConfig", new SimulationConfig { TickIntervalMs = 33, MaxRollbackTicks = 16 });
    Set(service, "_phase", SessionPhase.Playing);
    Get<Dictionary<int, int>>(service, "_peerToPlayer")[10] = 1;

    var smoothers = Get<IDictionary>(service, "_rttSmoothers");
    var smootherType = smoothers.GetType().GetGenericArguments()[1];
    var smoother = Activator.CreateInstance(smootherType);
    smoothers[1] = smoother;
    Sample(smoother, 628, 3);
    Call(service, "MaybePushExtraDelayUpdate", 1, 10);
    Get<Dictionary<int, int>>(service, "_lastPushedExtraDelay")[10].Should().Be(8);

    Call(service, "HandleReactiveExtraDelayReport", 10, new ReactiveExtraDelayReportMessage { EffectiveExtraDelay = 8 });
    Sample(smoother, 2, 5);
    Get<Dictionary<int, long>>(service, "_lastPushTimeMs").Clear();
    Call(service, "MaybePushExtraDelayUpdate", 1, 10);

    Get<Dictionary<int, int>>(service, "_lastPushedExtraDelay")[10].Should().Be(3);

    Call(service, "HandleReactiveExtraDelayReport", 10, new ReactiveExtraDelayReportMessage { EffectiveExtraDelay = 8 });
    Get<Dictionary<int, long>>(service, "_lastPushTimeMs").Clear();
    Call(service, "MaybePushExtraDelayUpdate", 1, 10);
    Get<Dictionary<int, int>>(service, "_lastPushedExtraDelay")[10].Should().Be(3);

    Sample(smoother, 628, 5);
    Get<Dictionary<int, long>>(service, "_lastPushTimeMs").Clear();
    Call(service, "MaybePushExtraDelayUpdate", 1, 10);
    Get<Dictionary<int, int>>(service, "_lastPushedExtraDelay")[10].Should().Be(8);
  }

  [Fact]
  public void LowerServerBaseline_PreservesLocalCorrectionUntilItDecays() {
    var engine = new KlothoEngine(new SimulationConfig { MaxRollbackTicks = 16 }, new SessionConfig());
    engine.ApplyExtraDelay(3, ExtraDelaySource.Sync);
    engine.EscalateExtraDelay(2, 4);
    engine.RecommendedExtraDelay.Should().Be(5);
    engine.ApplyExtraDelay(1, ExtraDelaySource.DynamicPush);
    engine.RecommendedExtraDelay.Should().Be(3);
    engine.DeEscalateExtraDelay(1);
    engine.RecommendedExtraDelay.Should().Be(2);
    engine.DeEscalateExtraDelay(1);
    engine.RecommendedExtraDelay.Should().Be(1);
  }

  private static void Sample(object smoother, int rtt, int count) {
    for (var i = 0; i < count; i++)
      smoother.GetType().GetMethod("OnSample").Invoke(smoother, [rtt]);
  }

  private static T Get<T>(object target, string name) => (T)Field(target, name).GetValue(target);
  private static void Set(object target, string name, object value) => Field(target, name).SetValue(target, value);
  private static FieldInfo Field(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
  private static void Call(object target, string name, params object[] args) =>
    target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
}
