# IIoT.UI.Themes

> IIoT/SCADA 시스템 — **WPF 공유 테마 라이브러리**  
> 7가지 테마 + 런타임 전환 + 자동 저장/복원

## Ver History

| 버전 | 날짜 | 내용 |
|------|------|------|
| v0.1 | 2025-05-16 | 프로젝트 구조 확정. DarkNavy + SteelLight, Styles 3종, ThemeManager |
| v1.0 | 2025-05-16 | 7개 테마 전체 구현. 키 일관성 검증 통과 |
| v1.1 | 2025-05-16 | ThemeSettingsService(저장/복원), ThemeTransitionService(페이드), ThemeSelectorViewModel, ThemeSelectorPanel UC 추가 |

---

## 프로젝트 구조

```
IIoT.UI.Themes/
├── IIoT.UI.Themes.csproj           OutputType=Library, GenerateLibraryLayout=true
│
├── ThemeManager.cs                 ★ 핵심 — 7테마 enum + Apply() + 메타정보
├── ThemeSettingsService.cs         JSON 저장/복원 (%AppData%\IIoT\ui-settings.json)
├── ThemeTransitionService.cs       페이드 전환 애니메이션 (180ms)
├── ThemeSelectorViewModel.cs       선택 UI ViewModel (NextTheme, PrevTheme)
│
├── Themes/                         ★ 테마 색상 정의 (7개, 키 이름 동일)
│   ├── Theme.DarkNavy.xaml         ① 기본 확정 — 우주 제어실  #4F7CFF
│   ├── Theme.SteelLight.xaml       ② 밝은 산업용              #0057D8
│   ├── Theme.NeonCyber.xaml        ③ 사이버펑크 네온           #00FFA3
│   ├── Theme.WarmAmber.xaml        ④ 황금 앰버 아날로그        #F59E0B
│   ├── Theme.ArcticFrost.xaml      ⑤ 북유럽 아이스 블루        #0284C7
│   ├── Theme.TerminalGreen.xaml    ⑥ CRT 레트로 터미널         #00FF41
│   └── Theme.CarbonElite.xaml      ⑦ 탄소섬유 골드 럭셔리      #C9A84C
│
├── Styles/                         공유 컨트롤 스타일 (DynamicResource 기반)
│   ├── Styles.Controls.xaml        버튼 5종 + TextBox + ComboBox + CheckBox
│   ├── Styles.Layout.xaml          카드 4종 + 배지 4종 + 통계카드 + 알람셀
│   └── Styles.TreeView.xaml        TreeView + TreeViewItem + 태그/알람 행
│
└── Controls/
    ├── ThemeSelectorPanel.xaml     카드형 테마 선택 UI (색상 미리보기)
    ├── ThemeSelectorPanel.xaml.cs  Code-behind + 변환기 3종
    ├── App.xaml.cs.full            각 WPF 프로그램 App.xaml.cs 완성 패턴
    ├── MainWindow.xaml.snippet     MainWindow 연동 XAML 예시
    └── MainWindow.xaml.cs.snippet  MainWindow 연동 코드 예시
```

---

## 각 WPF 프로그램에서 사용하는 방법

### 1) .csproj 설정

```xml
<ItemGroup>
  <ProjectReference Include="..\IIoT.UI.Themes\IIoT.UI.Themes.csproj"/>
</ItemGroup>
```

### 2) App.xaml.cs — 테마 초기화

```csharp
using IIoT.UI.Themes;

protected override void OnStartup(StartupEventArgs e)
{
    base.OnStartup(e);

    // 저장된 테마 복원 (없으면 DarkNavy 기본값)
    _themeSettings = new ThemeSettingsService();
    _themeSettings.LoadAndApply(this);

    // ... DI 구성, 윈도우 표시
}

protected override void OnExit(ExitEventArgs e)
{
    _themeSettings?.Dispose();  // 이벤트 구독 해제
    base.OnExit(e);
}
```

### 3) XAML에서 테마 리소스 사용

```xml
<!-- 항상 DynamicResource 사용 (StaticResource 사용 시 런타임 전환 불가) -->
<Border Background="{DynamicResource CardBrush}"
        BorderBrush="{DynamicResource Border2Brush}">

<Button Style="{DynamicResource PrimaryBtn}" Content="저장"/>
<TextBlock FontFamily="{DynamicResource MonoFont}"
           Foreground="{DynamicResource GreenBrush}"
           Text="150.3 °C"/>
```

### 4) 런타임 전환 3가지 방법

```csharp
// ① 즉시 전환
ThemeManager.Apply(ThemeKind.NeonCyber);

// ② 페이드 애니메이션 (180ms)
await ThemeTransitionService.ApplyWithTransitionAsync(
    ThemeKind.NeonCyber, rootGrid, Application.Current);

// ③ 단축키 순환 (ViewModel 통해)
_themeVm.NextThemeCommand.Execute(null);   // Ctrl+T
_themeVm.PrevThemeCommand.Execute(null);   // Ctrl+Shift+T
```

---

## 주요 테마 리소스 키 목록

| 키 | 설명 | DarkNavy 값 |
|----|------|-------------|
| `BgBrush` | 페이지 배경 | `#0B0D14` |
| `SurfaceBrush` | 패널/사이드바 | `#111520` |
| `CardBrush` | 카드/컨텐츠 박스 | `#161B2E` |
| `BorderBrush` | 구분선 (연함) | `#1E2540` |
| `Border2Brush` | 구분선 (강함) | `#2A3155` |
| `AccBrush` | 주 액센트 | `#4F7CFF` |
| `Acc2Brush` | 보조 액센트 | `#7C5CFF` |
| `GreenBrush` | 정상/성공 | `#22D3A0` |
| `YellowBrush` | 경고 | `#F5C842` |
| `RedBrush` | 위험/오류 | `#FF4F6B` |
| `OrangeBrush` | 주의 | `#FF8C42` |
| `TextBrush` | 주 텍스트 | `#E2E8F0` |
| `Text2Brush` | 보조 텍스트 | `#94A3B8` |
| `Text3Brush` | 흐린 텍스트 | `#4A5568` |
| `MonoFont` | 데이터/수치 폰트 | JetBrains Mono |
| `UiFont` | UI 폰트 | Segoe UI |
| `Radius` | 기본 모서리 반경 | `6` |

> ⚠️ **키 이름 변경 금지** — 모든 WPF 프로그램 XAML이 이 키를 참조한다.

---

## 새 테마 추가 방법

1. `Themes/Theme.[이름].xaml` 생성 — DarkNavy와 동일한 키 구조 유지
2. `ThemeKind` 열거형에 값 추가
3. `ThemeManager.ThemeUris` 딕셔너리에 URI 추가
4. `ThemeManager.AllThemes` 목록에 `ThemeInfo` 추가
5. `.csproj` `<Page>` 항목에 추가
