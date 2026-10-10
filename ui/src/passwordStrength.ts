// Parola gücü göstergesi: kayıt ve ilk kurulum formları aynı ölçüyü kullanır.

export const STRENGTH_LABELS = ["Çok zayıf", "Zayıf", "Orta", "İyi", "Güçlü"];
export const STRENGTH_COLORS = ["#ef4444", "#f97316", "#eab308", "#84cc16", "#16a34a"];

// A lightweight password-quality heuristic: length + character-class variety, 0..4.
export function passwordStrength(pw: string): number {
  if (!pw) return 0;
  let score = 0;
  if (pw.length >= 10) score++;
  if (pw.length >= 14) score++;
  if (/[a-z]/.test(pw) && /[A-Z]/.test(pw)) score++;
  if (/\d/.test(pw)) score++;
  if (/[^A-Za-z0-9]/.test(pw)) score++;
  return Math.min(score, 4);
}
