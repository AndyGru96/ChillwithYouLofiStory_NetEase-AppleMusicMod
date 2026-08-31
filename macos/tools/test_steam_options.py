import unittest
import steam_options as s

CONFIG='''// Keep comments and unrelated configuration unchanged.
"UserLocalConfigStore"
{
 "Software" { "Valve" { "Steam" { "apps" {
  "1" { "LaunchOptions" "other game" }
  "3548580"
  {
   "note" "中文与 { 花括号 }"
   "LaunchOptions" ""
  }
 } } } }
}
'''

class SteamOptionsTests(unittest.TestCase):
    def test_single_account_without_recent_marker(self):
        users=s.parse('"76561197960265729" { "RememberPassword" "1" }')
        self.assertIs(s.select_user(users),users[0])
    def test_multi_account_uses_only_unambiguous_recent_marker(self):
        users=s.parse('"1" { "MostRecent" "0" } "2" { "MostRecent" "1" }')
        self.assertIs(s.select_user(users),users[1])
        for source in ['"1" {} "2" {}','"1" { "MostRecent" "1" } "2" { "MostRecent" "1" }','']:
            with self.subTest(source=source):
                with self.assertRaises(RuntimeError): s.select_user(s.parse(source))
    def test_roundtrip_preserves_all_other_bytes(self):
        value='"/Users/test/space in path/launch-core.sh" --steam %command%'
        modified=s.set_option(CONFIG,value)
        self.assertEqual(s.read_option(modified),value)
        self.assertEqual(s.set_option(modified,''),CONFIG)
        self.assertIn('"LaunchOptions" "other game"',modified)
    def test_insert_missing_option(self):
        removed=s.set_option(CONFIG,None)
        self.assertIsNone(s.read_option(removed))
        self.assertEqual(s.read_option(s.set_option(removed,'new')),'new')
    def test_duplicate_target_rejected(self):
        with self.assertRaises(ValueError):s.read_option(CONFIG.replace('"LaunchOptions" ""','"LaunchOptions" "" "launchoptions" "bad"'))
    def test_invalid_syntax_rejected(self):
        for text in ['"a" {','"a" "b" }','not vdf']:
            with self.subTest(text=text):
                with self.assertRaises(ValueError):s.parse(text)

if __name__=='__main__':unittest.main()
