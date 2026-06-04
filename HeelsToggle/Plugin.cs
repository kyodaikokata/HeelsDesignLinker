using System.Numerics;
using System.Text.RegularExpressions;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Configuration;
using Dalamud.Bindings.ImGui;
using Newtonsoft.Json.Linq;

namespace HeelsToggle
{
    public enum PluginMode
    {
        Glamourer = 0,
        Penumbra = 1
    }

    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; } = 0;
        public List<HeelsRule> Rules { get; set; } = new();
        public int DecimalPrecision { get; set; } = 3; // 默认3位小数
        public PluginMode Mode { get; set; } = PluginMode.Glamourer; // 默认 Glamourer 模式
        
        // Penumbra 全局设置
        public string PenumbraCollection { get; set; } = "Default";
        public string PenumbraModName { get; set; } = "";
        public string PenumbraOption { get; set; } = "";
    }

    public class HeelsRule
    {
        public float MinHeight { get; set; } = 0.01f; 
        public float MaxHeight { get; set; } = 99.0f;  
        
        // Glamourer 设置
        public string GlamourerDesign { get; set; } = "";
        
        // Penumbra 设置
        public string PenumbraOptionName { get; set; } = ""; // 例如: Shoes, Barefoot, Heels
        
        public bool IsActive { get; set; } = true;
    }

    public class Plugin : IDalamudPlugin
    {
        public string Name => "Heels Glamourer Linker (Universal IPC)";

        [PluginService] public static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
        [PluginService] public static ICommandManager CommandManager { get; private set; } = null!;
        [PluginService] public static IFramework Framework { get; private set; } = null!;
        [PluginService] public static IObjectTable ObjectTable { get; private set; } = null!;
        [PluginService] public static IClientState ClientState { get; private set; } = null!;
        [PluginService] public static IPluginLog PluginLog { get; private set; } = null!;

        private Configuration Configuration { get; set; }
        private bool drawConfigUi = false;
        private string lastAppliedDesign = "";
        private float currentHeelsHeight = 0f;
        private int currentMatchedRuleIndex = -1; // 当前匹配的规则索引，-1 表示无匹配

        private bool isGlamourerAvailable = false;
        private bool isSimpleHeelsAvailable = false;
        private bool isSimpleHeelsIpcReady = false;
        
        private DateTime lastDependencyCheckUtc = DateTime.MinValue;
        private static readonly TimeSpan DependencyRecheckWhenMissing = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan DependencyRecheckWhenReady = TimeSpan.FromSeconds(30);

        /// <summary>本地玩家对象稳定就绪后，再等待一段时间才自动 apply，避免 &lt;me&gt; 未解析。</summary>
        private static readonly TimeSpan AutoApplyStartupDelay = TimeSpan.FromSeconds(3);
        private DateTime? localPlayerStableSinceUtc;
        private string applyGateStatus = "";
        
        // 调试信息
        private string lastError = "";
        private string lastIpcData = "";

        public Plugin()
        {
            Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            
            // 如果是首次运行(没有规则),添加默认规则模板
            if (Configuration.Rules.Count == 0)
            {
                Configuration.Rules.Add(new HeelsRule 
                { 
                    MinHeight = -1.00f, 
                    MaxHeight = -0.01f, 
                    GlamourerDesign = "SFX_Barefoot",
                    PenumbraOptionName = "Barefoot",
                    IsActive = true 
                });
                Configuration.Rules.Add(new HeelsRule 
                { 
                    MinHeight = -0.009f, 
                    MaxHeight = 0.03f, 
                    GlamourerDesign = "SFX_Shoes",
                    PenumbraOptionName = "Shoes",
                    IsActive = true 
                });
                Configuration.Rules.Add(new HeelsRule 
                { 
                    MinHeight = 0.031f, 
                    MaxHeight = 1.00f, 
                    GlamourerDesign = "SFX_Heels",
                    PenumbraOptionName = "Heels",
                    IsActive = true 
                });
                SaveConfig();
            }
            
            CommandManager.AddHandler("/hdl", new CommandInfo(OnCommand) { HelpMessage = Localization.CommandHelp });
            CommandManager.AddHandler("/heelsdesign", new CommandInfo(OnCommand) { HelpMessage = Localization.CommandHelp });
            
            RefreshDependencies(force: true);
            Framework.Update += OnFrameworkUpdate;
            ClientState.Logout += OnLogout;
            
            // 🎯 核心修正 2：注册标准界面渲染回调
            PluginInterface.UiBuilder.Draw += DrawConfigurationUI;
            
            // 🎯 核心修正 3：必须注册这个，告诉卫月当玩家在插件列表点击“设置”时应该呼出哪个 UI
            PluginInterface.UiBuilder.OpenConfigUi += () =>
            {
                drawConfigUi = true;
                RefreshDependencies(force: true);
            };
            PluginInterface.UiBuilder.OpenMainUi += () =>
            {
                drawConfigUi = true;
                RefreshDependencies(force: true);
            };
        }

        private void OnCommand(string command, string args)
        {
            drawConfigUi = !drawConfigUi;
            if (drawConfigUi)
                RefreshDependencies(force: true);
        }

        /// <summary>
        /// 依赖插件可能晚于本插件完成加载/注册 IPC，因此在就绪前周期性重检。
        /// </summary>
        private bool IsReadyForWork()
        {
            if (!isSimpleHeelsAvailable || !isSimpleHeelsIpcReady)
                return false;

            if (Configuration.Mode == PluginMode.Glamourer && !isGlamourerAvailable)
                return false;

            return true;
        }

        private void RefreshDependenciesIfNeeded()
        {
            var interval = IsReadyForWork() ? DependencyRecheckWhenReady : DependencyRecheckWhenMissing;
            var now = DateTime.UtcNow;
            if ((now - lastDependencyCheckUtc) < interval)
                return;

            RefreshDependencies(force: false);
        }

        private void RefreshDependencies(bool force)
        {
            if (!force)
            {
                var interval = IsReadyForWork() ? DependencyRecheckWhenReady : DependencyRecheckWhenMissing;
                var now = DateTime.UtcNow;
                if ((now - lastDependencyCheckUtc) < interval)
                    return;
            }

            lastDependencyCheckUtc = DateTime.UtcNow;
            var wasReady = IsReadyForWork();

            isGlamourerAvailable = PluginInterface.InstalledPlugins.Any(p =>
                p.InternalName == "Glamourer" && p.IsLoaded);
            isSimpleHeelsAvailable = PluginInterface.InstalledPlugins.Any(p =>
                p.InternalName == "SimpleHeels" && p.IsLoaded);

            isSimpleHeelsIpcReady = false;
            if (isSimpleHeelsAvailable)
            {
                try
                {
                    var provider = PluginInterface.GetIpcSubscriber<string>("SimpleHeels.GetLocalPlayer");
                    provider.InvokeFunc();
                    isSimpleHeelsIpcReady = true;
                }
                catch (Exception ex)
                {
                    PluginLog.Debug($"SimpleHeels IPC not ready yet: {ex.Message}");
                }
            }

            var nowReady = IsReadyForWork();
            if (nowReady && !wasReady)
            {
                PluginLog.Info(Configuration.Mode == PluginMode.Glamourer
                    ? "Heels Design Linker: SimpleHeels + Glamourer ready."
                    : "Heels Design Linker: SimpleHeels ready.");
            }
            else if (!nowReady && wasReady)
            {
                PluginLog.Warning("Heels Design Linker: dependency plugin(s) became unavailable.");
            }
        }

        private void OnLogout(int type, int code)
        {
            ResetApplyState();
        }

        private void ResetApplyState()
        {
            localPlayerStableSinceUtc = null;
            lastAppliedDesign = "";
        }

        private bool IsLocalPlayerReady()
        {
            if (!ClientState.IsLoggedIn)
                return false;

            var localPlayer = ObjectTable.LocalPlayer;
            return localPlayer != null && localPlayer.IsValid();
        }

        private bool CanAutoApply(out string gateStatus)
        {
            if (!ClientState.IsLoggedIn)
            {
                localPlayerStableSinceUtc = null;
                gateStatus = Localization.IsChine ? "未登录" : "Not logged in";
                return false;
            }

            if (!IsLocalPlayerReady())
            {
                localPlayerStableSinceUtc = null;
                gateStatus = Localization.IsChine ? "等待本地角色对象" : "Waiting for local player object";
                return false;
            }

            var now = DateTime.UtcNow;
            localPlayerStableSinceUtc ??= now;

            var elapsed = now - localPlayerStableSinceUtc.Value;
            if (elapsed < AutoApplyStartupDelay)
            {
                var remaining = AutoApplyStartupDelay - elapsed;
                gateStatus = Localization.IsChine
                    ? $"启动延迟 {remaining.TotalSeconds:F1}s"
                    : $"Startup delay {remaining.TotalSeconds:F1}s";
                return false;
            }

            gateStatus = Localization.IsChine ? "可自动应用" : "Ready to apply";
            return true;
        }

        private void OnFrameworkUpdate(IFramework framework)
        {
            RefreshDependenciesIfNeeded();
            if (!IsReadyForWork())
                return;

            if (!ClientState.IsLoggedIn)
            {
                applyGateStatus = Localization.IsChine ? "未登录" : "Not logged in";
                ResetApplyState();
                return;
            }
            
            try
            {
                // 📡 使用 SimpleHeels 的 IPC 获取本地玩家高度信息
                var getLocalPlayerProvider = PluginInterface.GetIpcSubscriber<string>("SimpleHeels.GetLocalPlayer");
                var localPlayerData = getLocalPlayerProvider.InvokeFunc();
                
                lastIpcData = localPlayerData ?? "NULL";
                
                // 解析返回的数据获取高度值
                if (!string.IsNullOrEmpty(localPlayerData))
                {
                    currentHeelsHeight = ParseHeelsHeight(localPlayerData);
                    lastError = ""; // 清除错误
                }
                else
                {
                    lastError = "SimpleHeels 返回空数据";
                }
            }
            catch (Exception ex)
            {
                lastError = $"IPC 错误: {ex.Message}";
                PluginLog.Error($"SimpleHeels IPC failed: {ex}");
                return;
            }

            // 规则判定
            string targetDesign = "";
            string targetOptionName = "";
            currentMatchedRuleIndex = -1; // 重置匹配索引
            
            for (int i = 0; i < Configuration.Rules.Count; i++)
            {
                var rule = Configuration.Rules[i];
                if (rule.IsActive && currentHeelsHeight >= rule.MinHeight && currentHeelsHeight <= rule.MaxHeight)
                {
                    currentMatchedRuleIndex = i; // 记录匹配的规则索引
                    if (Configuration.Mode == PluginMode.Glamourer)
                    {
                        targetDesign = rule.GlamourerDesign;
                    }
                    else // Penumbra
                    {
                        targetOptionName = rule.PenumbraOptionName;
                    }
                    break; 
                }
            }

            applyGateStatus = "";
            if (!CanAutoApply(out applyGateStatus))
                return;

            // 应用设计/设置
            if (Configuration.Mode == PluginMode.Glamourer)
            {
                if (targetDesign != lastAppliedDesign && !string.IsNullOrEmpty(targetDesign))
                {
                    try
                    {
                        CommandManager.ProcessCommand($"/glamour apply {targetDesign} | <me>");
                        lastAppliedDesign = targetDesign;
                        PluginLog.Info($"Applied Glamourer design: {targetDesign}");
                    }
                    catch (Exception ex)
                    {
                        PluginLog.Error($"Failed to apply Glamourer design: {ex.Message}");
                    }
                }
            }
            else // Penumbra
            {
                if (targetOptionName != lastAppliedDesign && !string.IsNullOrEmpty(targetOptionName))
                {
                    try
                    {
                        var collection = Configuration.PenumbraCollection;
                        var modName = Configuration.PenumbraModName;
                        var option = Configuration.PenumbraOption;
                        
                        CommandManager.ProcessCommand($"/penumbra mod setting {collection} | {modName} | {option} | {targetOptionName}");
                        lastAppliedDesign = targetOptionName;
                        PluginLog.Info($"Applied Penumbra setting: {targetOptionName}");
                    }
                    catch (Exception ex)
                    {
                        PluginLog.Error($"Failed to apply Penumbra setting: {ex.Message}");
                    }
                }
            }
        }
        
        // 解析 SimpleHeels 返回的高度数据
        private float ParseHeelsHeight(string data)
        {
            try
            {
                // 尝试使用 Newtonsoft.Json 解析
                var json = Newtonsoft.Json.Linq.JObject.Parse(data);
                
                // 正确字段是 DefaultOffset (根据实际数据结构)
                if (json["DefaultOffset"] is JToken offsetToken)
                {
                    var height = offsetToken.Value<float>();
                    PluginLog.Debug($"Parsed height from DefaultOffset: {height}");
                    return height;
                }
                
                // 备用方案:正则表达式匹配 DefaultOffset
                var match = Regex.Match(data, @"""DefaultOffset"":\s*(-?\d+\.?\d*)");
                if (match.Success && float.TryParse(match.Groups[1].Value, out var height2))
                {
                    PluginLog.Debug($"Parsed height (regex): {height2}");
                    return height2;
                }
                
                lastError = "无法找到 DefaultOffset 字段";
            }
            catch (Exception ex)
            {
                lastError = $"解析错误: {ex.Message}";
                PluginLog.Error($"Failed to parse height: {ex}");
            }
            return currentHeelsHeight; // 保持当前值
        }

        // UI 渲染
        private void DrawConfigurationUI()
        {
            if (!drawConfigUi) return;

            ImGui.SetNextWindowSize(new Vector2(600, 450), ImGuiCond.FirstUseEver);
            if (ImGui.Begin(Localization.WindowTitle, ref drawConfigUi))
            {
                if (!isGlamourerAvailable && Configuration.Mode == PluginMode.Glamourer)
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1.0f, 0.3f, 0.3f, 1.0f));
                    ImGui.TextWrapped($"⚠️ {Localization.GlamourerStatus}: {Localization.NotAvailable}");
                    ImGui.PopStyleColor();
                    ImGui.Separator();
                }
                
                if (!isSimpleHeelsAvailable || !isSimpleHeelsIpcReady)
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1.0f, 0.3f, 0.3f, 1.0f));
                    var shDetail = !isSimpleHeelsAvailable
                        ? Localization.NotAvailable
                        : (Localization.IsChine ? "已加载，IPC 未就绪" : "Loaded, IPC not ready");
                    ImGui.TextWrapped($"⚠️ {Localization.SimpleHeelsStatus}: {shDetail}");
                    ImGui.PopStyleColor();
                    ImGui.Separator();
                }

                // 标签页
                if (ImGui.BeginTabBar("MainTabs"))
                {
                    // 设置标签页
                    if (ImGui.BeginTabItem(Localization.TabSettings))
                    {
                        DrawSettingsTab();
                        ImGui.EndTabItem();
                    }
                    
                    // 调试标签页
                    if (ImGui.BeginTabItem(Localization.TabDebug))
                    {
                        DrawDebugTab();
                        ImGui.EndTabItem();
                    }
                    
                    // 更新履历
                    if (ImGui.BeginTabItem(Localization.TabChangelog))
                    {
                        DrawChangelogTab();
                        ImGui.EndTabItem();
                    }
                    
                    ImGui.EndTabBar();
                }
            }
            ImGui.End();
        }
        
        private void DrawSettingsTab()
        {
            // 当前高度显示
            ImGui.TextDisabled($"{Localization.CurrentHeight}: {currentHeelsHeight.ToString($"F{Configuration.DecimalPrecision}")}");
            
            // 显示当前匹配的规则
            if (currentMatchedRuleIndex >= 0)
            {
                ImGui.SameLine();
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.0f, 1.0f, 0.0f, 1.0f)); // 绿色
                ImGui.Text($"[{Localization.RuleMatched(currentMatchedRuleIndex)}]");
                ImGui.PopStyleColor();
            }
            else
            {
                ImGui.SameLine();
                ImGui.TextDisabled($"[{Localization.NoRuleMatched}]");
            }
            
            // 模式选择
            ImGui.Separator();
            ImGui.Text($"{Localization.Mode}:");
            ImGui.SameLine();
            
            int currentMode = (int)Configuration.Mode;
            if (ImGui.RadioButton(Localization.GlamourerMode, currentMode == 0))
            {
                Configuration.Mode = PluginMode.Glamourer;
                SaveConfig();
            }
            ImGui.SameLine();
            if (ImGui.RadioButton(Localization.PenumbraMode, currentMode == 1))
            {
                Configuration.Mode = PluginMode.Penumbra;
                SaveConfig();
            }
            
            // Penumbra 全局设置
            if (Configuration.Mode == PluginMode.Penumbra)
            {
                ImGui.Separator();
                ImGui.Text($"{Localization.PenumbraSettings}:");
                
                ImGui.SetNextItemWidth(200);
                string collection = Configuration.PenumbraCollection;
                if (ImGui.InputTextWithHint("##Collection", Localization.Collection, ref collection, 256))
                {
                    Configuration.PenumbraCollection = collection;
                    SaveConfig();
                }
                ImGui.SameLine();
                ImGui.TextDisabled(Localization.Collection);
                
                ImGui.SetNextItemWidth(200);
                string modName = Configuration.PenumbraModName;
                if (ImGui.InputTextWithHint("##ModName", Localization.ModName, ref modName, 256))
                {
                    Configuration.PenumbraModName = modName;
                    SaveConfig();
                }
                ImGui.SameLine();
                ImGui.TextDisabled(Localization.ModName);
                
                ImGui.SetNextItemWidth(200);
                string option = Configuration.PenumbraOption;
                if (ImGui.InputTextWithHint("##Option", Localization.Option, ref option, 256))
                {
                    Configuration.PenumbraOption = option;
                    SaveConfig();
                }
                ImGui.SameLine();
                ImGui.TextDisabled(Localization.Option);
            }
            
            ImGui.Separator();
            
            // 小数位数设置
            ImGui.SetNextItemWidth(200);
            int precision = Configuration.DecimalPrecision;
            if (ImGui.SliderInt($"{Localization.DecimalPrecision} ({Localization.Places})", ref precision, 0, 5))
            {
                Configuration.DecimalPrecision = precision;
                SaveConfig();
            }
            
            ImGui.Separator();
            
            if (Configuration.Mode == PluginMode.Glamourer)
            {
                ImGui.TextWrapped(Localization.GlamourerHint);
            }
            else
            {
                ImGui.TextWrapped(Localization.PenumbraHint);
            }
            
            ImGui.Separator();

            if (ImGui.Button(Localization.AddNewRule))
            {
                Configuration.Rules.Add(new HeelsRule());
            }

            ImGui.BeginChild("RulesList", new Vector2(0, -30), true, ImGuiWindowFlags.None);
            for (int i = 0; i < Configuration.Rules.Count; i++)
            {
                ImGui.PushID(i);
                var currentRule = Configuration.Rules[i];
                
                // 如果是当前匹配的规则，使用绿色背景高亮
                if (i == currentMatchedRuleIndex)
                {
                    ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(0.0f, 0.4f, 0.0f, 0.3f));
                    ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, new Vector4(0.0f, 0.5f, 0.0f, 0.4f));
                    ImGui.PushStyleColor(ImGuiCol.FrameBgActive, new Vector4(0.0f, 0.6f, 0.0f, 0.5f));
                }

                bool isActive = currentRule.IsActive;
                if (ImGui.Checkbox($"##{Localization.Active}", ref isActive)) 
                {
                    Configuration.Rules[i].IsActive = isActive;
                }
                
                ImGui.SameLine();
                ImGui.SetNextItemWidth(65);
                float minH = currentRule.MinHeight;
                string formatString = $"%.{Configuration.DecimalPrecision}f";
                if (ImGui.DragFloat(Localization.MinHeight, ref minH, 0.01f, -10f, 10f, formatString)) 
                {
                    Configuration.Rules[i].MinHeight = minH;
                }
                
                ImGui.SameLine();
                ImGui.SetNextItemWidth(65);
                float maxH = currentRule.MaxHeight;
                if (ImGui.DragFloat(Localization.MaxHeight, ref maxH, 0.01f, -10f, 10f, formatString)) 
                {
                    Configuration.Rules[i].MaxHeight = maxH;
                }
                
                ImGui.SameLine();
                ImGui.SetNextItemWidth(200);
                
                // 根据模式显示不同的输入框
                if (Configuration.Mode == PluginMode.Glamourer)
                {
                    string designName = currentRule.GlamourerDesign ?? "";
                    if (ImGui.InputTextWithHint("##DesignInput", Localization.DesignName, ref designName, 256))
                    {
                        Configuration.Rules[i].GlamourerDesign = designName;
                        SaveConfig();
                    }
                }
                else // Penumbra
                {
                    string optionName = currentRule.PenumbraOptionName ?? "";
                    if (ImGui.InputTextWithHint("##OptionInput", Localization.OptionName, ref optionName, 256))
                    {
                        Configuration.Rules[i].PenumbraOptionName = optionName;
                        SaveConfig();
                    }
                }
                
                ImGui.SameLine();
                if (ImGui.Button(Localization.Delete))
                {
                    Configuration.Rules.RemoveAt(i);
                    SaveConfig();
                }
                
                // 恢复高亮样式
                if (i == currentMatchedRuleIndex)
                {
                    ImGui.PopStyleColor(3);
                }

                ImGui.PopID();
            }
            ImGui.EndChild();

            if (ImGui.Button(Localization.Save)) SaveConfig();
        }
        
        private void DrawDebugTab()
        {
            ImGui.TextDisabled(Localization.TabDebug);
            ImGui.Separator();
            
            // 显示错误信息
            if (!string.IsNullOrEmpty(lastError))
            {
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1.0f, 0.3f, 0.3f, 1.0f));
                ImGui.TextWrapped($"{Localization.ErrorInfo}: {lastError}");
                ImGui.PopStyleColor();
                ImGui.Separator();
            }
            
            // IPC 测试按钮
            if (ImGui.Button(Localization.TestIPC))
            {
                try
                {
                    var provider = PluginInterface.GetIpcSubscriber<string>("SimpleHeels.GetLocalPlayer");
                    lastIpcData = provider.InvokeFunc();
                    PluginLog.Info($"IPC Data: {lastIpcData}");
                }
                catch (Exception ex)
                {
                    lastError = $"{Localization.TestIPC} {Localization.ErrorInfo}: {ex.Message}";
                    PluginLog.Error($"IPC test failed: {ex}");
                }
            }
            
            ImGui.Separator();
            
            // 状态信息
            var simpleHeelsStatus = !isSimpleHeelsAvailable
                ? $"✗ {Localization.NotAvailable}"
                : isSimpleHeelsIpcReady
                    ? $"✓ {Localization.Available}"
                    : (Localization.IsChine ? "△ 已加载 / IPC 等待中" : "△ Loaded / IPC pending");
            ImGui.Text($"{Localization.SimpleHeelsStatus}: {simpleHeelsStatus}");
            
            if (Configuration.Mode == PluginMode.Glamourer)
            {
                ImGui.Text($"{Localization.GlamourerStatus}: {(isGlamourerAvailable ? $"✓ {Localization.Available}" : $"✗ {Localization.NotAvailable}")}");
            }
            else
            {
                ImGui.Text($"{Localization.PenumbraMode} ({(Localization.IsChine ? "使用命令接口" : "Using Command Interface")})");
            }
            
            ImGui.Text($"{Localization.CurrentMode}: {Configuration.Mode}");
            ImGui.Text($"{Localization.LastApplied}: {lastAppliedDesign}");
            ImGui.Text($"{Localization.ApplyGate}: {applyGateStatus}");
            
            ImGui.Separator();
            
            // 显示原始 IPC 数据
            if (!string.IsNullOrEmpty(lastIpcData))
            {
                if (ImGui.CollapsingHeader(Localization.RawIPCData))
                {
                    ImGui.TextWrapped(lastIpcData);
                }
            }
            
            ImGui.Separator();
            
            // Penumbra 命令示例
            if (Configuration.Mode == PluginMode.Penumbra && ImGui.CollapsingHeader(Localization.PenumbraFormat))
            {
                ImGui.TextWrapped($"{(Localization.IsChine ? "完整命令格式" : "Full Command Format")}:");
                ImGui.TextDisabled($"/penumbra mod setting {Configuration.PenumbraCollection} | {Configuration.PenumbraModName} | {Configuration.PenumbraOption} | <OptionName>");
                
                ImGui.Spacing();
                ImGui.TextWrapped($"{(Localization.IsChine ? "示例" : "Examples")}:");
                ImGui.BulletText($"/penumbra mod setting {Configuration.PenumbraCollection} | {Configuration.PenumbraModName} | {Configuration.PenumbraOption} | Shoes");
                ImGui.BulletText($"/penumbra mod setting {Configuration.PenumbraCollection} | {Configuration.PenumbraModName} | {Configuration.PenumbraOption} | Barefoot");
                ImGui.BulletText($"/penumbra mod setting {Configuration.PenumbraCollection} | {Configuration.PenumbraModName} | {Configuration.PenumbraOption} | Heels");
            }
            
            // Glamourer 支持格式
            if (Configuration.Mode == PluginMode.Glamourer && ImGui.CollapsingHeader(Localization.GlamourerFormat))
            {
                ImGui.TextWrapped(Localization.GlamourerHint);
            }
        }

        private void DrawChangelogTab()
        {
            ImGui.BeginChild("ChangelogScroll", new Vector2(0, 0), false, ImGuiWindowFlags.None);
            
            foreach (var entry in Changelog.Entries)
            {
                var isCurrent = entry.Version == Changelog.CurrentVersion;
                if (isCurrent)
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.4f, 1.0f, 0.5f, 1.0f));
                }
                
                ImGui.Text($"{Localization.ChangelogVersion} {entry.Version}  ·  {Localization.ChangelogDate} {entry.Date}");
                if (isCurrent)
                {
                    ImGui.PopStyleColor();
                }
                
                var items = Localization.IsChine ? entry.ItemsZh : entry.ItemsEn;
                foreach (var item in items)
                {
                    ImGui.BulletText(item);
                }
                
                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();
            }
            
            ImGui.EndChild();
        }

        private void SaveConfig() => PluginInterface.SavePluginConfig(Configuration);

        public void Dispose()
        {
            ClientState.Logout -= OnLogout;
            Framework.Update -= OnFrameworkUpdate;
            CommandManager.RemoveHandler("/hdl");
            CommandManager.RemoveHandler("/heelsdesign");
        }
    }
}