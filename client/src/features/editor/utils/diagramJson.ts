// Chỗ trống trong khung JSON có dạng <...>, ví dụ "<Actor 1>"
const BLANK_PATTERN = /<[A-Za-z][^<>"\n]*>/g;

// "title" đầu tiên trong JSON là tiêu đề của diagram
const ROOT_TITLE_PATTERN = /("title"\s*:\s*)"(?:[^"\\]|\\.)*"/;

export interface JsonBlank {
  start: number;
  end: number;
  text: string;
}

export function findBlanks(json: string): JsonBlank[] {
  return Array.from(json.matchAll(BLANK_PATTERN), (match) => ({
    start: match.index ?? 0,
    end: (match.index ?? 0) + match[0].length,
    text: match[0],
  }));
}

// Tìm chỗ trống kế tiếp sau vị trí con trỏ; hết thì quay lại chỗ trống đầu tiên
export function findNextBlank(json: string, fromIndex: number): JsonBlank | null {
  const blanks = findBlanks(json);
  return blanks.find((blank) => blank.start >= fromIndex) ?? blanks[0] ?? null;
}

// Ghi đè giá trị "title" đầu tiên bằng tiêu đề diagram, giữ nguyên định dạng phần còn lại của JSON
export function syncJsonTitle(json: string, title: string): string {
  return ROOT_TITLE_PATTERN.test(json)
    ? json.replace(ROOT_TITLE_PATTERN, (_match, prefix: string) => `${prefix}${JSON.stringify(title)}`)
    : json;
}
