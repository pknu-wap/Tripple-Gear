using UnityEngine;
using TMPro; // TextMeshPro를 사용하기 위한 패키지

public class ClickCounter : MonoBehaviour
{
    // 인스펙터창에서 텍스트 컴포넌트를 연결할 수 있게 함
    public TextMeshProUGUI numberText;

    // 숫자를 저장할 변수
    private int count = 0;

    // 버튼을 누를 때 실행될 함수
    public void IncreaseNumber()
    {
        count++; // 숫자를 1 증가
        numberText.text = count.ToString(); // 화면의 텍스트 변경
    }
}
