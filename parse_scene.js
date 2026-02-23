const fs = require('fs');
const content = fs.readFileSync('c:/Users/hashi/Test Project/Assets/Scenes/05_Game.unity', 'utf8');

// Build GameObject fileID -> m_Name map
const goNames = {};
const goRegex = /--- !u!1 &(\d+)([\s\S]*?)(?=--- !u!)/g;
let m;
while ((m = goRegex.exec(content)) !== null) {
    const fid = m[1];
    const block = m[2];
    const nameMatch = block.match(/m_Name: (.+)/);
    if (nameMatch) {
        goNames[fid] = nameMatch[1].trim();
    }
}

// Build RectTransform fileID -> m_GameObject fileID map
const rtToGo = {};
const rtRegex = /--- !u!224 &(\d+)([\s\S]*?)(?=--- !u!)/g;
while ((m = rtRegex.exec(content)) !== null) {
    const rtFid = m[1];
    const block = m[2];
    const goRef = block.match(/m_GameObject: \{fileID: (\d+)/);
    if (goRef) {
        rtToGo[rtFid] = goRef[1];
    }
}

// Lookup child fileIDs
const cards = {
    'SkillCard1': ['529980333', '1315131274', '2085969776', '1995632049'],
    'SkillCard2': ['611232065', '1805329400', '429917468', '1779937952'],
    'SkillCard3': ['1150383482', '875093029', '1854765171', '2070500163'],
};

for (const [card, ids] of Object.entries(cards)) {
    console.log('\n' + card + ':');
    for (const cid of ids) {
        const goFid = rtToGo[cid];
        if (goFid) {
            const name = goNames[goFid] || '[name not found, GO fid=' + goFid + ']';
            console.log('  fileID=' + cid + ' -> GO fid=' + goFid + ' -> Name=' + name);
        } else {
            console.log('  fileID=' + cid + ' -> [RectTransform block not found]');
        }
    }
}
console.log('\nTotal GameObjects found: ' + Object.keys(goNames).length);
console.log('Total RectTransforms found: ' + Object.keys(rtToGo).length);
