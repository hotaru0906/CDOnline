using System.Collections.Generic;
using UnityEngine;

public class ProfanityFilter : MonoBehaviour
{
    // Danh sách từ cấm (thêm từ của bạn vào đây)
    private static List<string> badWords = new List<string>
    {
        // Tiếng Việt
        "dm", "đm", "dcm", "đcm", "vl", "cc", "lol", "shit", "fuck", 
        "đĩ", "mẹ", "con chó", "ngu", "óc chó", "đần", "khùng",
        "địt", "buồi", "lồn", "cặc", "đéo", "đụ", "cứt",
        
        // Tiếng Anh
        "fuck", "shit", "damn", "bitch", "ass", "hell", "dick",
        "pussy", "cock", "cunt", "bastard", "slut", "whore"
        
        // Thêm từ khác tùy ý...
    };

    // Kiểm tra chuỗi có chứa từ tục tĩu không
    public static bool ContainsProfanity(string text)
    {
        if (string.IsNullOrEmpty(text))
            return false;

        string lowerText = text.ToLower();

        foreach (string badWord in badWords)
        {
            if (lowerText.Contains(badWord.ToLower()))
            {
                return true;
            }
        }

        return false;
    }

    // Lấy thông báo lỗi
    public static string GetErrorMessage()
    {
        return "Tên phòng chứa từ ngữ không phù hợp!";
    }
}