using UnityEngine;

public enum NPCLanguage { English, Thai }

/// <summary>
/// ชุด dialogue ครบชุดสำหรับ 1 ภาษา — ใช้คู่กับ ARQuestNPC
/// Placeholder: {food} {target} {monster} {amount} {days} {npcName} {count} {progress} {tierRequirement}
/// </summary>
[System.Serializable]
public class NPCDialogueSet
{
    [Header("Messages")]
    [TextArea(2, 4)] public string offerMessage;
    [TextArea(2, 3)] public string acceptedMessage;
    [TextArea(2, 3)] public string declinedMessage;
    [TextArea(2, 3)] public string notEnoughMessage;
    [TextArea(2, 3)] public string alreadyAcceptedMessage;
    [TextArea(2, 3)] public string onCooldownMessage;

    [Header("Button Labels")]
    public string okButtonLabel;
    public string acceptButtonLabel;
    public string declineButtonLabel;

    [Header("Quest Objective")]
    [Tooltip("ข้อความ Objective ที่แสดงใน Quest Log")]
    [TextArea(1, 2)] public string objectiveText;

    [Header("Letter Templates")]
    [Tooltip("Placeholder: {npcName}")]
    [TextArea(1, 2)] public string letterSenderTemplate;

    [Tooltip("Placeholder: {npcName}, {food}/{target}, {amount}")]
    [TextArea(4, 8)] public string letterContentTemplate;

    // ── Default factories — no-arg (DeliverItem) ────────────────────────

    public static NPCDialogueSet DefaultEnglish() => DefaultEnglish(QuestType.DeliverItem);
    public static NPCDialogueSet DefaultThai()    => DefaultThai(QuestType.DeliverItem);

    // ── Default factories — per QuestType ───────────────────────────────

    public static NPCDialogueSet DefaultEnglish(QuestType questType) => questType switch
    {
        QuestType.CookAndDeliver => new NPCDialogueSet
        {
            offerMessage           = "Could you cook me some {food} for {amount} gold?\nI'll pay you back in {days} days, I promise!",
            acceptedMessage        = "I can't wait to taste it! Bring it to me when it's ready.",
            declinedMessage        = "Oh, alright. No worries then.",
            notEnoughMessage       = "You don't have {food} ready yet... Come back once it's cooked!",
            alreadyAcceptedMessage = "Has the {food} been cooked yet? Bring it over when it's ready!",
            onCooldownMessage      = "Thanks for the meal! I'll reach out when I'm hungry again.",
            okButtonLabel          = "OK",
            acceptButtonLabel      = "OK",
            declineButtonLabel     = "NO",
            objectiveText          = "Cook {food} and deliver it to {npcName}.",
            letterSenderTemplate   = "From: {npcName}",
            letterContentTemplate  = "Hello!\n\nThank you so much for the delicious {food}! Here is {amount} gold as promised.\n\nSincerely,\n{npcName}",
        },

        QuestType.KillMonster => new NPCDialogueSet
        {
            offerMessage           = "There are {count} {target} causing trouble nearby!\nCould you deal with them for {amount} gold? I'll pay in {days} days!\nRequired tier: {tierRequirement}",
            acceptedMessage        = "Thank you! I'm counting on you. Come back when they're taken care of.",
            declinedMessage        = "Oh, I understand. It's a tough job.",
            notEnoughMessage       = "You haven't finished yet... Keep going!\n{progress} / {count} {target} defeated.\nRequired tier: {tierRequirement}",
            alreadyAcceptedMessage = "Still at it? You've defeated {progress} out of {count} {target} so far!\nRequired tier: {tierRequirement}",
            onCooldownMessage      = "Thanks for your help! I'll let you know if trouble comes again.",
            okButtonLabel          = "OK",
            acceptButtonLabel      = "OK",
            declineButtonLabel     = "NO",
            objectiveText          = "Defeat {count} {target} for {npcName}. (Required tier: {tierRequirement})",
            letterSenderTemplate   = "From: {npcName}",
            letterContentTemplate  = "Hello!\n\nThank you for dealing with those {target}! Here is {amount} gold as promised.\n\nSincerely,\n{npcName}",
        },

        QuestType.CollectItem => new NPCDialogueSet
        {
            offerMessage           = "I need {count} {target} for something important.\nCould you gather them for {amount} gold? I'll pay in {days} days!",
            acceptedMessage        = "Wonderful! Bring them to me when you've collected them all.",
            declinedMessage        = "Oh, alright. No problem.",
            notEnoughMessage       = "You don't have enough {target} yet...\nYou need {count} in total!",
            alreadyAcceptedMessage = "Have you gathered all {count} {target} yet? Bring them over when you're ready!",
            onCooldownMessage      = "Thanks for gathering those! I'll let you know if I need more.",
            okButtonLabel          = "OK",
            acceptButtonLabel      = "OK",
            declineButtonLabel     = "NO",
            objectiveText          = "Collect {count} {target} for {npcName}.",
            letterSenderTemplate   = "From: {npcName}",
            letterContentTemplate  = "Hello!\n\nThank you for gathering those {target}! Here is {amount} gold as promised.\n\nSincerely,\n{npcName}",
        },

        _ => new NPCDialogueSet // DeliverItem
        {
            offerMessage           = "Hey there! Could I get a {food} for {amount} gold?\nI'll pay you back in {days} days, I promise!",
            acceptedMessage        = "You're a lifesaver! I'll send the money by mail. Thank you!",
            declinedMessage        = "Oh, alright. No worries, I understand.",
            notEnoughMessage       = "It seems you don't have enough {food} right now...\nCome back when you have it!",
            alreadyAcceptedMessage = "Thanks again for the food! I'll have your money sent over soon!",
            onCooldownMessage      = "Thanks again! I'll let you know when I need something.",
            okButtonLabel          = "OK",
            acceptButtonLabel      = "OK",
            declineButtonLabel     = "NO",
            objectiveText          = "Deliver {food} to {npcName}.",
            letterSenderTemplate   = "From: {npcName}",
            letterContentTemplate  = "Hello!\n\nThank you so much for the {food} the other day. Here is {amount} gold as promised.\n\nSincerely,\n{npcName}",
        },
    };

    public static NPCDialogueSet DefaultThai(QuestType questType) => questType switch
    {
        QuestType.CookAndDeliver => new NPCDialogueSet
        {
            offerMessage           = "คุณช่วยปรุง {food} ให้ฉันได้ไหม ในราคา {amount} เหรียญทอง?\nฉันจะจ่ายคืนใน {days} วัน สัญญา!",
            acceptedMessage        = "รอดูอยู่นะ! เอามาให้ฉันเมื่อปรุงเสร็จแล้ว",
            declinedMessage        = "โอเค ไม่เป็นไร ไม่ต้องกังวล",
            notEnoughMessage       = "คุณยังไม่มี {food} ที่ปรุงเสร็จ... กลับมาเมื่อพร้อมแล้ว!",
            alreadyAcceptedMessage = "{food} ปรุงเสร็จหรือยัง? เอามาให้ฉันได้เลยนะ!",
            onCooldownMessage      = "ขอบคุณสำหรับอาหาร! ฉันจะติดต่อใหม่เมื่อหิวอีกครั้ง",
            okButtonLabel          = "ตกลง",
            acceptButtonLabel      = "รับ",
            declineButtonLabel     = "ปฏิเสธ",
            objectiveText          = "ปรุง {food} และนำไปส่งที่ {npcName}",
            letterSenderTemplate   = "จาก: {npcName}",
            letterContentTemplate  = "สวัสดี!\n\nขอบคุณมากสำหรับ {food} อร่อยมากเลย!\nนี่คือ {amount} เหรียญทองตามที่สัญญาไว้\n\nด้วยความนับถือ\n{npcName}",
        },

        QuestType.KillMonster => new NPCDialogueSet
        {
            offerMessage           = "มี {target} จำนวน {count} ตัวก่อกวนอยู่แถวนี้!\nคุณช่วยจัดการได้ไหม แลกกับ {amount} เหรียญทอง? ฉันจะจ่ายภายใน {days} วัน!\nระดับที่นับ: {tierRequirement}",
            acceptedMessage        = "ขอบคุณ! ฉันฝากไว้กับคุณนะ กลับมาเมื่อจัดการเสร็จแล้ว",
            declinedMessage        = "เข้าใจนะ มันเป็นงานที่ยากจริงๆ",
            notEnoughMessage       = "ยังไม่เสร็จหรอ... สู้ต่อไป!\n{progress} / {count} ตัวแล้ว\nระดับที่นับ: {tierRequirement}",
            alreadyAcceptedMessage = "ยังไม่เสร็จเหรอ? ตอนนี้กำจัดได้ {progress} จาก {count} {target} แล้ว!\nระดับที่นับ: {tierRequirement}",
            onCooldownMessage      = "ขอบคุณที่ช่วยเหลือ! ฉันจะบอกถ้ามีปัญหาอีก",
            okButtonLabel          = "ตกลง",
            acceptButtonLabel      = "รับ",
            declineButtonLabel     = "ปฏิเสธ",
            objectiveText          = "กำจัด {target} จำนวน {count} ตัวให้ {npcName} (ระดับที่นับ: {tierRequirement})",
            letterSenderTemplate   = "จาก: {npcName}",
            letterContentTemplate  = "สวัสดี!\n\nขอบคุณที่จัดการกับ {target} พวกนั้น!\nนี่คือ {amount} เหรียญทองตามที่สัญญาไว้\n\nด้วยความนับถือ\n{npcName}",
        },

        QuestType.CollectItem => new NPCDialogueSet
        {
            offerMessage           = "ฉันต้องการ {target} จำนวน {count} ชิ้นสำหรับบางสิ่งสำคัญ\nคุณช่วยหาได้ไหม แลกกับ {amount} เหรียญทอง? ฉันจะจ่ายภายใน {days} วัน!",
            acceptedMessage        = "เยี่ยมมาก! เอามาให้ฉันเมื่อเก็บครบแล้ว",
            declinedMessage        = "โอเค ไม่เป็นไร",
            notEnoughMessage       = "คุณยังมี {target} ไม่พอ...\nต้องการ {count} ชิ้นรวมทั้งหมด!",
            alreadyAcceptedMessage = "เก็บ {target} ครบ {count} ชิ้นหรือยัง? เอามาให้ฉันได้เลย!",
            onCooldownMessage      = "ขอบคุณที่เก็บมาให้! ฉันจะบอกถ้าต้องการอีก",
            okButtonLabel          = "ตกลง",
            acceptButtonLabel      = "รับ",
            declineButtonLabel     = "ปฏิเสธ",
            objectiveText          = "เก็บ {target} จำนวน {count} ชิ้นให้ {npcName}",
            letterSenderTemplate   = "จาก: {npcName}",
            letterContentTemplate  = "สวัสดี!\n\nขอบคุณที่เก็บ {target} มาให้!\nนี่คือ {amount} เหรียญทองตามที่สัญญาไว้\n\nด้วยความนับถือ\n{npcName}",
        },

        _ => new NPCDialogueSet // DeliverItem
        {
            offerMessage           = "สวัสดี! ฉันขอรับ {food} ในราคา {amount} เหรียญทองได้ไหม?\nฉันจะจ่ายคืนใน {days} วัน สัญญา!",
            acceptedMessage        = "คุณช่วยชีวิตฉันไว้เลย! ฉันจะส่งเงินทางไปรษณีย์ ขอบคุณมาก!",
            declinedMessage        = "โอเค ไม่เป็นไร ฉันเข้าใจ",
            notEnoughMessage       = "ดูเหมือนตอนนี้คุณมี {food} ไม่พอ...\nกลับมาใหม่เมื่อคุณมีแล้ว!",
            alreadyAcceptedMessage = "ขอบคุณสำหรับอาหารอีกครั้ง! เดี๋ยวฉันจะโอนเงินให้เร็วๆ นี้!",
            onCooldownMessage      = "ขอบคุณอีกครั้ง! ฉันจะแจ้งให้คุณทราบเมื่อฉันต้องการอะไร",
            okButtonLabel          = "ตกลง",
            acceptButtonLabel      = "รับ",
            declineButtonLabel     = "ปฏิเสธ",
            objectiveText          = "ส่ง {food} ให้กับ {npcName}",
            letterSenderTemplate   = "จาก: {npcName}",
            letterContentTemplate  = "สวัสดี!\n\nขอบคุณมากสำหรับ {food} เมื่อวันก่อน\nนี่คือ {amount} เหรียญทองตามที่สัญญาไว้\n\nด้วยความนับถือ\n{npcName}",
        },
    };
}
