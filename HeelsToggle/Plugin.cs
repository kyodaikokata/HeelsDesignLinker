using System.Numerics;
using System.Text.RegularExpressions;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Configuration;
using Dalamud.Bindings.ImGui;

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
        public string PenumbraModName { get; set; } = "Immersive Footsteps";
        public string PenumbraOption { get; set; } = "Footsteps";
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
        [PluginService] public static IPluginLog PluginLog { get; private set; } = null!;

        private Configuration Configuration { get; set; }
        private bool drawConfigUi = false;
        private string lastAppliedDesign = "";
        private float currentHeelsHeight = 0f;

        private bool isGlamourerAvailable = false;
        private bool isSimpleHeelsAvailable = false;
        
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
                    MinHeight = -0.01f, 
                    MaxHeight = 0.05f, 
                    GlamourerDesign = "SFX_Shoes",
                    PenumbraOptionName = "Shoes",
                    IsActive = true 
                });
                Configuration.Rules.Add(new HeelsRule 
                { 
                    MinHeight = 0.05f, 
                    MaxHeight = 99.00f, 
                    GlamourerDesign = "SFX_Heels",
                    PenumbraOptionName = "Heels",
                    IsActive = true 
                });
                SaveConfig();
            }
            
            CommandManager.AddHandler("/hgl", new CommandInfo(OnCommand) { HelpMessage = "打开 Heels Glamourer 联动配置" });
            
            CheckDependencies();
            Framework.Update += OnFrameworkUpdate;
            
            // 🎯 核心修正 2：注册标准界面渲染回调
            PluginInterface.UiBuilder.Draw += DrawConfigurationUI;
            
            // 🎯 核心修正 3：必须注册这个，告诉卫月当玩家在插件列表点击“设置”时应该呼出哪个 UI
            PluginInterface.UiBuilder.OpenConfigUi += () => drawConfigUi = true;
            PluginInterface.UiBuilder.OpenMainUi += () => drawConfigUi = true; // 消除 main ui 警告
        }

        private void OnCommand(string command, string args) => drawConfigUi = !drawConfigUi;

        private void CheckDependencies()
        {
            isGlamourerAvailable = PluginInterface.InstalledPlugins.Any(p => p.InternalName == "Glamourer" && p.IsLoaded);
            isSimpleHeelsAvailable = PluginInterface.InstalledPlugins.Any(p => p.InternalName == "SimpleHeels" && p.IsLoaded);
        }

        private void OnFrameworkUpdate(IFramework framework)
        {
            if (!isGlamourerAvailable || !isSimpleHeelsAvailable) return;
            
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
            
            foreach (var rule in Configuration.Rules)
            {
                if (rule.IsActive && currentHeelsHeight >= rule.MinHeight && currentHeelsHeight <= rule.MaxHeight)
                {
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
                if (json["DefaultOffset"] != null)
                {
                    var height = json["DefaultOffset"].ToObject<float>();
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
            if (ImGui.Begin("Heels Design Linker", ref drawConfigUi))
            {
                if (!isGlamourerAvailable && Configuration.Mode == PluginMode.Glamourer)
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1.0f, 0.3f, 0.3f, 1.0f));
                    ImGui.TextWrapped("⚠️ 错误：Glamourer 未启用！");
                    ImGui.PopStyleColor();
                    ImGui.Separator();
                }
                
                if (!isSimpleHeelsAvailable)
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1.0f, 0.3f, 0.3f, 1.0f));
                    ImGui.TextWrapped("⚠️ 错误：SimpleHeels 未启用！");
                    ImGui.PopStyleColor();
                    ImGui.Separator();
                }

                // 标签页
                if (ImGui.BeginTabBar("MainTabs"))
                {
                    // 设置标签页
                    if (ImGui.BeginTabItem("设置"))
                    {
                        DrawSettingsTab();
                        ImGui.EndTabItem();
                    }
                    
                    // 调试标签页
                    if (ImGui.BeginTabItem("调试"))
                    {
                        DrawDebugTab();
                        ImGui.EndTabItem();
                    }
                    
                    ImGui.EndTabBar();
                }
            }
            ImGui.End();
        }
        
        private void DrawSettingsTab()
        {
            ImGui.TextDisabled($"当前鞋跟高度: {currentHeelsHeight.ToString($"F{Configuration.DecimalPrecision}")}");
            
            // 模式选择
            ImGui.Separator();
            ImGui.Text("工作模式:");
            ImGui.SameLine();
            
            int currentMode = (int)Configuration.Mode;
            if (ImGui.RadioButton("Glamourer", currentMode == 0))
            {
                Configuration.Mode = PluginMode.Glamourer;
                SaveConfig();
            }
            ImGui.SameLine();
            if (ImGui.RadioButton("Penumbra", currentMode == 1))
            {
                Configuration.Mode = PluginMode.Penumbra;
                SaveConfig();
            }
            
            // Penumbra 全局设置
            if (Configuration.Mode == PluginMode.Penumbra)
            {
                ImGui.Separator();
                ImGui.Text("Penumbra 设置:");
                
                ImGui.SetNextItemWidth(200);
                string collection = Configuration.PenumbraCollection;
                if (ImGui.InputTextWithHint("##Collection", "Collection 名称", ref collection, 256))
                {
                    Configuration.PenumbraCollection = collection;
                    SaveConfig();
                }
                ImGui.SameLine();
                ImGui.TextDisabled("Collection");
                
                ImGui.SetNextItemWidth(200);
                string modName = Configuration.PenumbraModName;
                if (ImGui.InputTextWithHint("##ModName", "Mod 名称", ref modName, 256))
                {
                    Configuration.PenumbraModName = modName;
                    SaveConfig();
                }
                ImGui.SameLine();
                ImGui.TextDisabled("Mod 名称");
                
                ImGui.SetNextItemWidth(200);
                string option = Configuration.PenumbraOption;
                if (ImGui.InputTextWithHint("##Option", "选项名称", ref option, 256))
                {
                    Configuration.PenumbraOption = option;
                    SaveConfig();
                }
                ImGui.SameLine();
                ImGui.TextDisabled("选项");
            }
            
            ImGui.Separator();
            
            // 小数位数设置
            ImGui.SetNextItemWidth(200);
            int precision = Configuration.DecimalPrecision;
            if (ImGui.SliderInt("小数位数", ref precision, 0, 5))
            {
                Configuration.DecimalPrecision = precision;
                SaveConfig();
            }
            
            ImGui.Separator();
            
            if (Configuration.Mode == PluginMode.Glamourer)
            {
                ImGui.TextWrapped("提示: 直接输入 Glamourer 设计名称 (不区分大小写)");
            }
            else
            {
                ImGui.TextWrapped("提示: 输入 Penumbra Option Name (例如: Shoes, Barefoot, Heels)");
            }
            
            ImGui.Separator();

            if (ImGui.Button("添加新规则 (Add Rule)"))
            {
                Configuration.Rules.Add(new HeelsRule());
            }

            ImGui.BeginChild("RulesList", new Vector2(0, -30), true, ImGuiWindowFlags.None);
            for (int i = 0; i < Configuration.Rules.Count; i++)
            {
                ImGui.PushID(i);
                var currentRule = Configuration.Rules[i];

                bool isActive = currentRule.IsActive;
                if (ImGui.Checkbox("##Active", ref isActive)) Configuration.Rules[i].IsActive = isActive;
                
                ImGui.SameLine();
                ImGui.SetNextItemWidth(65);
                float minH = currentRule.MinHeight;
                string formatString = $"%.{Configuration.DecimalPrecision}f";
                if (ImGui.DragFloat("最小", ref minH, 0.01f, -10f, 10f, formatString)) Configuration.Rules[i].MinHeight = minH;
                
                ImGui.SameLine();
                ImGui.SetNextItemWidth(65);
                float maxH = currentRule.MaxHeight;
                if (ImGui.DragFloat("最大", ref maxH, 0.01f, -10f, 10f, formatString)) Configuration.Rules[i].MaxHeight = maxH;
                
                ImGui.SameLine();
                ImGui.SetNextItemWidth(200);
                
                // 根据模式显示不同的输入框
                if (Configuration.Mode == PluginMode.Glamourer)
                {
                    string designName = currentRule.GlamourerDesign ?? "";
                    if (ImGui.InputTextWithHint("##DesignInput", "Glamourer 设计名称", ref designName, 256))
                    {
                        Configuration.Rules[i].GlamourerDesign = designName;
                        SaveConfig();
                    }
                }
                else // Penumbra
                {
                    string optionName = currentRule.PenumbraOptionName ?? "";
                    if (ImGui.InputTextWithHint("##OptionInput", "Penumbra Option Name", ref optionName, 256))
                    {
                        Configuration.Rules[i].PenumbraOptionName = optionName;
                        SaveConfig();
                    }
                }
                
                ImGui.SameLine();
                if (ImGui.Button("删除"))
                {
                    Configuration.Rules.RemoveAt(i);
                    SaveConfig();
                }

                ImGui.PopID();
            }
            ImGui.EndChild();

            if (ImGui.Button("保存配置 (Save)")) SaveConfig();
        }
        
        private void DrawDebugTab()
        {
            ImGui.TextDisabled("调试工具和信息");
            ImGui.Separator();
            
            // 显示错误信息
            if (!string.IsNullOrEmpty(lastError))
            {
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1.0f, 0.3f, 0.3f, 1.0f));
                ImGui.TextWrapped($"错误: {lastError}");
                ImGui.PopStyleColor();
                ImGui.Separator();
            }
            
            // IPC 测试按钮
            if (ImGui.Button("测试 SimpleHeels IPC"))
            {
                try
                {
                    var provider = PluginInterface.GetIpcSubscriber<string>("SimpleHeels.GetLocalPlayer");
                    lastIpcData = provider.InvokeFunc();
                    PluginLog.Info($"IPC Data: {lastIpcData}");
                }
                catch (Exception ex)
                {
                    lastError = $"IPC 测试失败: {ex.Message}";
                    PluginLog.Error($"IPC test failed: {ex}");
                }
            }
            
            ImGui.Separator();
            
            // 状态信息
            ImGui.Text($"SimpleHeels 状态: {(isSimpleHeelsAvailable ? "✓ 已连接" : "✗ 未连接")}");
            
            if (Configuration.Mode == PluginMode.Glamourer)
            {
                ImGui.Text($"Glamourer 状态: {(isGlamourerAvailable ? "✓ 已连接" : "✗ 未连接")}");
            }
            else
            {
                ImGui.Text("Penumbra 模式 (使用命令接口)");
            }
            
            ImGui.Text($"当前模式: {Configuration.Mode}");
            ImGui.Text($"最后应用: {lastAppliedDesign}");
            
            ImGui.Separator();
            
            // 显示原始 IPC 数据
            if (!string.IsNullOrEmpty(lastIpcData))
            {
                if (ImGui.CollapsingHeader("原始 IPC 数据"))
                {
                    ImGui.TextWrapped(lastIpcData);
                }
            }
            
            ImGui.Separator();
            
            // Penumbra 命令示例
            if (Configuration.Mode == PluginMode.Penumbra && ImGui.CollapsingHeader("Penumbra 命令示例"))
            {
                ImGui.TextWrapped($"完整命令格式:");
                ImGui.TextDisabled($"/penumbra mod setting {Configuration.PenumbraCollection} | {Configuration.PenumbraModName} | {Configuration.PenumbraOption} | <OptionName>");
                
                ImGui.Spacing();
                ImGui.TextWrapped("示例:");
                ImGui.BulletText($"/penumbra mod setting {Configuration.PenumbraCollection} | {Configuration.PenumbraModName} | {Configuration.PenumbraOption} | Shoes");
                ImGui.BulletText($"/penumbra mod setting {Configuration.PenumbraCollection} | {Configuration.PenumbraModName} | {Configuration.PenumbraOption} | Barefoot");
                ImGui.BulletText($"/penumbra mod setting {Configuration.PenumbraCollection} | {Configuration.PenumbraModName} | {Configuration.PenumbraOption} | Heels");
            }
            
            // Glamourer 支持格式
            if (Configuration.Mode == PluginMode.Glamourer && ImGui.CollapsingHeader("Glamourer 支持格式"))
            {
                ImGui.BulletText("设计名称 (不区分大小写)");
                ImGui.BulletText("设计路径 (用 / 分隔文件夹)");
                ImGui.BulletText("设计标识符 (至少4个字符)");
            }
        }

        private void SaveConfig() => PluginInterface.SavePluginConfig(Configuration);

        public void Dispose()
        {
            Framework.Update -= OnFrameworkUpdate;
            CommandManager.RemoveHandler("/hgl");
        }
    }
}