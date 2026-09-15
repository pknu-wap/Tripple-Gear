# 샷게임 한글 인스펙터

`KoreanLabel`을 붙이면 C# 변수명은 그대로 두고 Unity Inspector에 보이는 필드 이름만 한글로 바꿀 수 있습니다.

```csharp
[SerializeField, KoreanLabel("대기 그래픽")]
private Transform idleGraphic;
```

필드의 실제 직렬화 이름은 그대로 유지되므로 기존 씬과 프리팹 연결은 끊기지 않습니다.
